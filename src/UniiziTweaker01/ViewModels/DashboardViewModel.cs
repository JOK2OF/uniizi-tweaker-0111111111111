using UniiziTweaker01.Core;
using UniiziTweaker01.Infrastructure;
using UniiziTweaker01.Services;

namespace UniiziTweaker01.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private const int SlowRefreshEvery = 15;

    private readonly SystemMonitorService _monitor;
    private readonly DisplayService _display = new();
    private readonly SampleHistory _cpuHistory = new(60);
    private readonly SampleHistory _ramHistory = new(60);
    private CancellationTokenSource? _cts;
    private int _tick;

    private string _cpuText = "Measuring…";
    private double _cpuPercent;
    private string _ramText = "…";
    private string _ramSubtitle = "";
    private double _ramPercent;
    private string _diskText = "…";
    private string _diskSubtitle = "";
    private double _diskPercent;
    private string _processText = "…";
    private string _uptimeText = "…";
    private string _displayText = "…";
    private string _displaySubtitle = "";
    private string _powerPlanText = "…";
    private string _startupText = "…";
    private string _lastUpdateText = "Waiting for first measurement…";
    private bool _isPaused;
    private double[] _cpuSeries = Array.Empty<double>();
    private double[] _ramSeries = Array.Empty<double>();

    public DashboardViewModel(SystemMonitorService monitor)
    {
        _monitor = monitor;
        WindowsText = SystemInfoProvider.GetWindowsVersion();
        TogglePauseCommand = new RelayCommand(_ => IsPaused = !IsPaused);
    }

    public RelayCommand TogglePauseCommand { get; }

    /// <summary>Set when the window is minimized: sampling is skipped to save resources.</summary>
    public bool Suspended { get; set; }

    public string WindowsText { get; }
    public string CpuSubtitle => "Total across all cores";
    public string GpuText => "Unavailable";
    public string GpuSubtitle => "GPU load, memory and temperature need a hardware-sensor library (planned). No value is invented.";
    public string TemperatureText => "Unavailable";
    public string TemperatureSubtitle => "CPU/GPU temperatures need a hardware-sensor library and are not read yet.";

    public string CpuText { get => _cpuText; private set => Set(ref _cpuText, value); }
    public double CpuPercent { get => _cpuPercent; private set => Set(ref _cpuPercent, value); }
    public string RamText { get => _ramText; private set => Set(ref _ramText, value); }
    public string RamSubtitle { get => _ramSubtitle; private set => Set(ref _ramSubtitle, value); }
    public double RamPercent { get => _ramPercent; private set => Set(ref _ramPercent, value); }
    public string DiskText { get => _diskText; private set => Set(ref _diskText, value); }
    public string DiskSubtitle { get => _diskSubtitle; private set => Set(ref _diskSubtitle, value); }
    public double DiskPercent { get => _diskPercent; private set => Set(ref _diskPercent, value); }
    public string ProcessText { get => _processText; private set => Set(ref _processText, value); }
    public string UptimeText { get => _uptimeText; private set => Set(ref _uptimeText, value); }
    public string DisplayText { get => _displayText; private set => Set(ref _displayText, value); }
    public string DisplaySubtitle { get => _displaySubtitle; private set => Set(ref _displaySubtitle, value); }
    public string PowerPlanText { get => _powerPlanText; private set => Set(ref _powerPlanText, value); }
    public string StartupText { get => _startupText; private set => Set(ref _startupText, value); }
    public string LastUpdateText { get => _lastUpdateText; private set => Set(ref _lastUpdateText, value); }
    public double[] CpuHistory { get => _cpuSeries; private set => Set(ref _cpuSeries, value); }
    public double[] RamHistory { get => _ramSeries; private set => Set(ref _ramSeries, value); }

    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            if (Set(ref _isPaused, value))
            {
                Raise(nameof(PauseButtonText));
                if (value) LastUpdateText += " · paused";
            }
        }
    }

    public string PauseButtonText => IsPaused ? "Resume monitoring" : "Pause monitoring";

    public void Start()
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        await RefreshAsync();
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (IsPaused || Suspended) continue;
                await RefreshAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            var snapshot = await Task.Run(_monitor.Sample);
            ApplySnapshot(snapshot);

            if (_tick++ % SlowRefreshEvery == 0)
            {
                var slow = await Task.Run(() =>
                {
                    var (primary, count) = _display.GetPrimarySummary();
                    return (Plan: SystemInfoProvider.GetActivePowerPlan(), Startup: SystemInfoProvider.GetStartupEntryCount(),
                            Primary: primary, Count: count);
                });
                PowerPlanText = slow.Plan ?? "Unavailable";
                StartupText = slow.Startup is int n ? n.ToString() : "Unavailable";
                DisplayText = slow.Primary?.ToString() ?? "Unavailable";
                DisplaySubtitle = $"{slow.Count} connected display(s)";
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Dashboard refresh failed", ex);
            LastUpdateText = "Measurement failed: " + ex.Message;
        }
    }

    private void ApplySnapshot(SystemSnapshot s)
    {
        if (s.CpuPercent is double cpu)
        {
            CpuPercent = cpu;
            CpuText = $"{cpu:0}%";
            _cpuHistory.Add(cpu);
            CpuHistory = _cpuHistory.ToArray();
        }

        RamPercent = s.RamPercent;
        RamText = $"{ByteFormatter.Format(s.UsedRamBytes)} / {ByteFormatter.Format(s.TotalRamBytes)}";
        RamSubtitle = $"{s.RamPercent:0}% used · {ByteFormatter.Format(s.AvailableRamBytes)} available";
        _ramHistory.Add(s.RamPercent);
        RamHistory = _ramHistory.ToArray();

        if (s.DiskUsedPercent is double disk && s.SystemDriveTotalBytes is long total && s.SystemDriveFreeBytes is long free)
        {
            DiskPercent = disk;
            DiskText = $"{disk:0}% used";
            DiskSubtitle = $"{ByteFormatter.Format(free)} free of {ByteFormatter.Format(total)} (system drive)";
        }
        else
        {
            DiskText = "Unavailable";
            DiskSubtitle = "The system drive could not be read.";
        }

        ProcessText = s.ProcessCount.ToString();
        UptimeText = UptimeFormatter.Format(s.Uptime);
        LastUpdateText = $"Last update: {s.Timestamp:HH:mm:ss}";
    }
}
