using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using UniiziTweaker01.Core;
using UniiziTweaker01.Infrastructure;
using UniiziTweaker01.Services;

namespace UniiziTweaker01.ViewModels;

public sealed class ResolutionViewModel : ObservableObject
{
    public const int ConfirmSeconds = 15;

    private sealed record ApplyOutcome(string? Error, DisplayMode? Previous);

    private readonly DisplayService _service;
    private readonly DispatcherTimer _timer;
    private RollbackCountdown? _countdown;
    private DisplayDeviceInfo? _selectedDisplay;
    private ResolutionOption? _selectedOption;
    private int? _selectedRate;
    private bool _suspend;
    private bool _busy;
    private bool _pending;
    private string _status = "";
    private string _countdownText = "";
    private string _infoText = "";
    private DisplayMode? _previousMode;
    private DisplayMode? _targetMode;
    private string? _pendingDevice;

    public ResolutionViewModel(DisplayService service)
    {
        _service = service;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => OnTimerTick();

        RefreshCommand = new RelayCommand(_ => _ = RefreshAsync(), _ => !_busy && !_pending);
        ApplyCommand = new RelayCommand(_ => _ = ApplyAsync(), _ => CanApply());
        KeepCommand = new RelayCommand(_ => _ = KeepAsync(), _ => _pending && !_busy);
        RevertCommand = new RelayCommand(_ => _ = RevertAsync(false), _ => _pending && !_busy);
        OpenWindowsSettingsCommand = new RelayCommand(_ => OpenWindowsDisplaySettings());
    }

    public ObservableCollection<DisplayDeviceInfo> Displays { get; } = new();
    public ObservableCollection<ResolutionOption> Options { get; } = new();
    public ObservableCollection<int> RefreshRates { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand KeepCommand { get; }
    public ICommand RevertCommand { get; }
    public ICommand OpenWindowsSettingsCommand { get; }

    public DisplayDeviceInfo? SelectedDisplay
    {
        get => _selectedDisplay;
        set
        {
            if (Set(ref _selectedDisplay, value) && !_suspend && !_pending) LoadDisplay(value);
        }
    }

    public ResolutionOption? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (!Set(ref _selectedOption, value) || _suspend || value == null || _selectedDisplay == null) return;
            _suspend = true;
            try
            {
                FillRates(_selectedDisplay, value, _selectedRate ?? _selectedDisplay.Current.RefreshRate);
                Raise(nameof(SelectedRefreshRate));
            }
            finally { _suspend = false; }
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public int? SelectedRefreshRate
    {
        get => _selectedRate;
        set
        {
            if (Set(ref _selectedRate, value) && !_suspend) CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool PendingConfirmation { get => _pending; private set => Set(ref _pending, value); }
    public string StatusText { get => _status; private set => Set(ref _status, value); }
    public string CountdownText { get => _countdownText; private set => Set(ref _countdownText, value); }
    public string InfoText { get => _infoText; private set => Set(ref _infoText, value); }

    public string GameVsDesktopNote =>
        "This changes the Windows desktop resolution. Games use their own in-game resolution setting " +
        "(often exclusive full screen), which is independent from the desktop mode.";

    public string SafetyNote =>
        "Only modes reported by Windows and your display driver are listed; unsupported modes cannot be selected. " +
        "A tested mode is applied for this session first. Unless you press Keep, it is reverted automatically after " +
        ConfirmSeconds + " seconds. Custom resolutions through the GPU driver are not implemented in this version.";

    public void Initialize() => _ = RefreshAsync();

    /// <summary>Called on window close: never leave an unconfirmed mode applied.</summary>
    public void Shutdown()
    {
        if (!_pending || _pendingDevice == null || _previousMode == null) return;
        _timer.Stop();
        _service.ApplyTemporary(_pendingDevice, _previousMode);
        AppLog.Info("Unconfirmed display change reverted on shutdown");
    }

    public async Task RefreshAsync()
    {
        if (_busy) return;
        SetBusy(true);
        StatusText = "Reading displays…";
        try
        {
            var displays = await Task.Run(() => _service.GetDisplays());
            string? keep = _selectedDisplay?.DeviceName;
            _suspend = true;
            try
            {
                Displays.Clear();
                foreach (var d in displays) Displays.Add(d);
                var pick = displays.FirstOrDefault(d => d.DeviceName == keep)
                           ?? displays.FirstOrDefault(d => d.IsPrimary)
                           ?? displays.FirstOrDefault();
                _selectedDisplay = pick;
                Raise(nameof(SelectedDisplay));
                LoadDisplay(pick);
            }
            finally { _suspend = false; }
            StatusText = displays.Count == 0
                ? "Windows reported no active displays."
                : $"{displays.Count} display(s) detected.";
        }
        catch (Exception ex)
        {
            AppLog.Error("Display refresh failed", ex);
            StatusText = "Could not read displays: " + ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void LoadDisplay(DisplayDeviceInfo? display)
    {
        bool previous = _suspend;
        _suspend = true;
        try
        {
            Options.Clear();
            RefreshRates.Clear();
            _selectedOption = null;
            _selectedRate = null;
            if (display != null)
            {
                foreach (var o in DisplayModeCatalog.BuildOptions(display.Modes)) Options.Add(o);
                var current = display.Current;
                _selectedOption = Options.FirstOrDefault(o => o.Resolution == current.Resolution);
                FillRates(display, _selectedOption, current.RefreshRate);
            }
            Raise(nameof(SelectedOption));
            Raise(nameof(SelectedRefreshRate));
            InfoText = display == null ? "" : BuildInfo(display);
        }
        finally
        {
            _suspend = previous;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void FillRates(DisplayDeviceInfo display, ResolutionOption? option, int? preferred)
    {
        RefreshRates.Clear();
        _selectedRate = null;
        if (option == null) return;
        foreach (int rate in DisplayModeCatalog.RefreshRatesFor(display.Modes, option.Resolution)) RefreshRates.Add(rate);
        _selectedRate = DisplayModeCatalog.PickRefreshRate(display.Modes, option.Resolution, preferred);
    }

    private string BuildInfo(DisplayDeviceInfo d) =>
        $"Monitor: {d.MonitorName}\nGraphics adapter: {d.AdapterName}\nDevice: {d.DeviceName}\n" +
        $"Primary: {(d.IsPrimary ? "yes" : "no")}\nCurrent mode: {d.Current} ({d.Current.BitsPerPixel}-bit)\n" +
        $"Connected displays: {Displays.Count}";

    private bool CanApply()
    {
        if (_busy || _pending || _selectedDisplay == null || _selectedOption == null || _selectedRate == null) return false;
        var cur = _selectedDisplay.Current;
        return !(cur.Width == _selectedOption.Resolution.Width && cur.Height == _selectedOption.Resolution.Height &&
                 cur.RefreshRate == _selectedRate);
    }

    private async Task ApplyAsync()
    {
        var display = _selectedDisplay;
        var option = _selectedOption;
        int? rate = _selectedRate;
        if (display == null || option == null || rate == null) return;

        var target = new DisplayMode(option.Resolution.Width, option.Resolution.Height, rate.Value, display.Current.BitsPerPixel);
        if (!DisplayModeCatalog.IsSupported(display.Modes, target))
        {
            StatusText = $"Rejected: {target} is not in the list of modes reported by Windows.";
            return;
        }

        string device = display.DeviceName;
        SetBusy(true);
        StatusText = $"Testing {target}…";
        ApplyOutcome outcome;
        try
        {
            outcome = await Task.Run(() =>
            {
                var previous = _service.GetCurrent(device);
                if (previous == null) return new ApplyOutcome("Could not read the current display mode.", null);

                var test = _service.TestMode(device, target);
                if (test != DisplayChangeResult.Success)
                    return new ApplyOutcome($"Windows rejected this mode ({test}). Nothing was changed.", null);

                var applied = _service.ApplyTemporary(device, target);
                if (applied != DisplayChangeResult.Success)
                {
                    _service.ApplyTemporary(device, previous);
                    return new ApplyOutcome($"Applying the mode failed ({applied}). The previous mode was restored.", null);
                }

                var now = _service.GetCurrent(device);
                if (now == null || now.Width != target.Width || now.Height != target.Height || now.RefreshRate != target.RefreshRate)
                {
                    _service.ApplyTemporary(device, previous);
                    return new ApplyOutcome("Windows did not report the requested mode after applying it. The previous mode was restored.", null);
                }
                return new ApplyOutcome(null, previous);
            });
        }
        catch (Exception ex)
        {
            AppLog.Error("Apply display mode failed", ex);
            outcome = new ApplyOutcome("Unexpected error: " + ex.Message, null);
        }

        SetBusy(false);

        if (outcome.Error != null)
        {
            StatusText = outcome.Error;
            await RefreshAsync();
            return;
        }

        _previousMode = outcome.Previous;
        _targetMode = target;
        _pendingDevice = device;
        _countdown = new RollbackCountdown(ConfirmSeconds);
        CountdownText = $"Reverting in {_countdown.Remaining} s…";
        PendingConfirmation = true;
        StatusText = $"{target} applied temporarily and verified. Press Keep to save it.";
        _timer.Start();
        CommandManager.InvalidateRequerySuggested();
    }

    private async Task KeepAsync()
    {
        if (!_pending || _pendingDevice == null || _targetMode == null) return;
        _timer.Stop();
        string device = _pendingDevice;
        var target = _targetMode;
        SetBusy(true);
        var result = await Task.Run(() => _service.ApplyPersistent(device, target));
        SetBusy(false);

        if (result == DisplayChangeResult.Success)
        {
            ClearPending();
            StatusText = $"{target} was saved as your Windows display mode.";
            await RefreshAsync();
        }
        else
        {
            StatusText = $"Saving failed ({result}); reverting to the previous mode.";
            await RevertAsync(false);
        }
    }

    private async Task RevertAsync(bool automatic)
    {
        _timer.Stop();
        string? device = _pendingDevice;
        var previous = _previousMode;
        if (device == null || previous == null)
        {
            ClearPending();
            return;
        }

        SetBusy(true);
        var result = await Task.Run(() => _service.ApplyTemporary(device, previous));
        SetBusy(false);
        ClearPending();

        StatusText = result == DisplayChangeResult.Success
            ? (automatic ? "No confirmation received: the previous display mode was restored automatically."
                         : "The previous display mode was restored.")
            : $"The previous mode could not be restored automatically ({result}). Use Windows display settings.";
        await RefreshAsync();
    }

    private void ClearPending()
    {
        PendingConfirmation = false;
        _pendingDevice = null;
        _previousMode = null;
        _targetMode = null;
        _countdown = null;
        CountdownText = "";
        CommandManager.InvalidateRequerySuggested();
    }

    private void OnTimerTick()
    {
        if (_countdown == null) return;
        _countdown.Tick();
        CountdownText = $"Reverting in {_countdown.Remaining} s…";
        if (_countdown.Expired) _ = RevertAsync(true);
    }

    private void SetBusy(bool value)
    {
        _busy = value;
        CommandManager.InvalidateRequerySuggested();
    }

    private static void OpenWindowsDisplaySettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:display") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Error("Could not open Windows display settings", ex);
        }
    }
}
