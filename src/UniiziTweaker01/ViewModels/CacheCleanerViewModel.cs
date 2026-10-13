#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using UniiziTweaker01.Core.CacheCleaning;
using UniiziTweaker01.Services;

namespace UniiziTweaker01.ViewModels;

public abstract class CleanerNotifyBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

public sealed class CleanerCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private readonly Action<Exception>? _onError;

    public CleanerCommand(Func<Task> execute, Func<bool>? canExecute = null, Action<Exception>? onError = null)
    {
        _execute = execute;
        _canExecute = canExecute;
        _onError = onError;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter)
    {
        try
        {
            await _execute();
        }
        catch (Exception ex)
        {
            _onError?.Invoke(ex);
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class CleanerCategoryItem : CleanerNotifyBase
{
    private readonly Action _selectionChanged;
    private bool _isSelected;
    private bool _canEdit = true;
    private string _resultText = "Not scanned yet.";

    public CleanerCategoryItem(CacheCategory category, Action selectionChanged)
    {
        Category = category;
        _isSelected = category.SelectedByDefault;
        _selectionChanged = selectionChanged;
    }

    public CacheCategory Category { get; }

    public string Name => Category.Name;

    public string Description => Category.Description;

    public string Note => Category.Note;

    public bool HasNote => Category.Note.Length > 0;

    public bool IsCaution => Category.Risk != CategoryRisk.Safe;

    public string RiskText => Category.Risk switch
    {
        CategoryRisk.Safe => "Safe",
        CategoryRisk.Caution => "Use with care",
        _ => "Needs your explicit confirmation",
    };

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (Set(ref _isSelected, value)) _selectionChanged();
        }
    }

    public bool CanEdit
    {
        get => _canEdit;
        set => Set(ref _canEdit, value);
    }

    public string ResultText
    {
        get => _resultText;
        set => Set(ref _resultText, value);
    }
}

public sealed class CacheCleanerViewModel : CleanerNotifyBase
{
    private readonly CacheCleanerService _service = new CacheCleanerService();
    private CancellationTokenSource? _cts;
    private bool _isBusy;
    private bool _scanValid;
    private bool _scanHasWork;
    private bool _showConfirm;
    private bool _showReport;
    private bool _recycleAck;
    private bool _recycleSelected;
    private bool _historyEmpty = true;
    private string _status = "Choose what to scan, then press Scan. Nothing is deleted until you confirm.";
    private string _progressText = string.Empty;
    private string _scanSummary = string.Empty;
    private string _confirmText = string.Empty;

    public CacheCleanerViewModel()
    {
        foreach (var c in CacheCategoryCatalog.All)
        {
            Categories.Add(new CleanerCategoryItem(c, OnSelectionChanged));
        }

        ScanCommand = new CleanerCommand(ScanAsync, () => !IsBusy, OnError);
        CleanCommand = new CleanerCommand(() => { BeginConfirm(); return Task.CompletedTask; }, () => !IsBusy && _scanValid && _scanHasWork && !ShowConfirm, OnError);
        ConfirmCommand = new CleanerCommand(CleanAsync, () => !IsBusy && ShowConfirm && (!RecycleBinSelected || RecycleAck), OnError);
        BackCommand = new CleanerCommand(() => { ShowConfirm = false; return Task.CompletedTask; }, () => !IsBusy && ShowConfirm, OnError);
        CancelCommand = new CleanerCommand(() => { _cts?.Cancel(); Status = "Cancelling..."; return Task.CompletedTask; }, () => IsBusy, OnError);

        RecycleBinSelected = Categories.Any(c => c.IsSelected && c.Category.IsRecycleBin);
        RefreshHistory(_service.LoadHistory());
    }

    public ObservableCollection<CleanerCategoryItem> Categories { get; } = new ObservableCollection<CleanerCategoryItem>();

    public ObservableCollection<string> ReportLines { get; } = new ObservableCollection<string>();

    public ObservableCollection<string> HistoryLines { get; } = new ObservableCollection<string>();

    public CleanerCommand ScanCommand { get; }

    public CleanerCommand CleanCommand { get; }

    public CleanerCommand ConfirmCommand { get; }

    public CleanerCommand BackCommand { get; }

    public CleanerCommand CancelCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!Set(ref _isBusy, value)) return;
            foreach (var c in Categories) c.CanEdit = !value;
            RefreshCommands();
        }
    }

    public bool ShowConfirm
    {
        get => _showConfirm;
        private set
        {
            if (Set(ref _showConfirm, value)) RefreshCommands();
        }
    }

    public bool ShowReport
    {
        get => _showReport;
        private set => Set(ref _showReport, value);
    }

    public bool RecycleBinSelected
    {
        get => _recycleSelected;
        private set
        {
            if (Set(ref _recycleSelected, value)) RefreshCommands();
        }
    }

    public bool RecycleAck
    {
        get => _recycleAck;
        set
        {
            if (Set(ref _recycleAck, value)) RefreshCommands();
        }
    }

    public bool HistoryEmpty
    {
        get => _historyEmpty;
        private set => Set(ref _historyEmpty, value);
    }

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public string ProgressText
    {
        get => _progressText;
        private set => Set(ref _progressText, value);
    }

    public string ScanSummary
    {
        get => _scanSummary;
        private set => Set(ref _scanSummary, value);
    }

    public string ConfirmText
    {
        get => _confirmText;
        private set => Set(ref _confirmText, value);
    }

    private List<CacheCategory> SelectedCategories() =>
        Categories.Where(c => c.IsSelected).Select(c => c.Category).ToList();

    private void RefreshCommands()
    {
        ScanCommand.RaiseCanExecuteChanged();
        CleanCommand.RaiseCanExecuteChanged();
        ConfirmCommand.RaiseCanExecuteChanged();
        BackCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
    }

    private void OnSelectionChanged()
    {
        RecycleBinSelected = Categories.Any(c => c.IsSelected && c.Category.IsRecycleBin);
        if (!RecycleBinSelected) RecycleAck = false;
        if (_scanValid)
        {
            _scanValid = false;
            ShowConfirm = false;
            Status = "Selection changed - scan again before cleaning.";
        }
        RefreshCommands();
    }

    private void OnError(Exception ex)
    {
        _scanValid = false;
        Status = "Unexpected error: " + ex.Message;
        IsBusy = false;
        RefreshCommands();
    }

    private void OnProgress(CleanProgress p)
    {
        ProgressText = p.Phase + " - " + p.CategoryName + ": " + p.Items + " items checked, " + SizeText.Format(p.Bytes) + " so far";
    }

    private async Task ScanAsync()
    {
        var selected = SelectedCategories();
        if (selected.Count == 0)
        {
            Status = "Select at least one category first.";
            return;
        }

        ShowReport = false;
        ShowConfirm = false;
        ScanSummary = string.Empty;
        _scanValid = false;
        foreach (var c in Categories) c.ResultText = c.IsSelected ? "Scanning..." : "Not selected.";

        IsBusy = true;
        Status = "Scanning... nothing is deleted during a scan.";
        _cts = new CancellationTokenSource();
        try
        {
            var run = await _service.ScanAsync(selected, new Progress<CleanProgress>(OnProgress), _cts.Token);
            ApplyScan(run);
        }
        catch (Exception ex)
        {
            _scanValid = false;
            Status = "Scan failed: " + ex.Message;
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            ProgressText = string.Empty;
            IsBusy = false;
        }
    }

    private void ApplyScan(CleanupRun run)
    {
        foreach (var r in run.Results)
        {
            var item = Categories.FirstOrDefault(c => c.Category.Id == r.CategoryId);
            if (item == null) continue;

            if (item.Category.IsRecycleBin)
            {
                item.ResultText = r.Message;
                continue;
            }

            var text = r.Tally.Files == 0
                ? "Nothing to remove."
                : r.Tally.Files + " files, " + SizeText.Format(r.Tally.Bytes) + " can be removed";
            if (r.Tally.SkippedItems > 0) text += " (" + r.Tally.SkippedItems + " items will be skipped)";
            if (r.Message.Length > 0) text += " - " + r.Message;
            item.ResultText = text;
        }

        foreach (var c in Categories.Where(c => c.IsSelected && run.Results.All(r => r.CategoryId != c.Category.Id)))
        {
            c.ResultText = "Not scanned (scan was cancelled).";
        }

        var totals = CleanupTotals.From(run.Results);
        _scanHasWork = totals.Files > 0;
        _scanValid = !run.Cancelled;

        if (run.Cancelled)
        {
            ScanSummary = "Scan cancelled - results are incomplete. Scan again before cleaning.";
            Status = "Scan cancelled.";
        }
        else
        {
            ScanSummary = "Estimate: " + totals.Files + " files, " + SizeText.Format(totals.Bytes)
                + " can be removed. This is the logical size of the files measured now; the real space freed can differ, and files in use will be skipped.";
            Status = _scanHasWork
                ? "Scan complete. Review the estimate, then press Clean selected."
                : "Scan complete: nothing to remove in the selected categories.";
        }
        RefreshCommands();
    }

    private void BeginConfirm()
    {
        var selected = Categories.Where(c => c.IsSelected).ToList();
        var sb = new StringBuilder();
        sb.AppendLine("You are about to permanently delete files from:");
        foreach (var c in selected) sb.AppendLine(" - " + c.Name + ": " + c.ResultText);
        sb.AppendLine();
        sb.AppendLine("Deleted files do not go to the Recycle Bin and this app cannot restore them. Files in use are skipped.");
        if (selected.Any(c => c.Category.Id.StartsWith("browser-", StringComparison.Ordinal)))
        {
            sb.AppendLine("Close the browsers you selected first, otherwise many of their cache files will be skipped.");
        }
        if (selected.Any(c => c.Category.Id == "shader-cache"))
        {
            sb.AppendLine("Shader caches are rebuilt by games: expect brief stutter the first time you start each game afterwards.");
        }
        ConfirmText = sb.ToString().TrimEnd();
        RecycleAck = false;
        ShowConfirm = true;
    }

    private async Task CleanAsync()
    {
        if (RecycleBinSelected && !RecycleAck) return;
        var selected = SelectedCategories();
        if (selected.Count == 0) return;

        ShowConfirm = false;
        ShowReport = false;
        IsBusy = true;
        Status = "Cleaning...";
        _cts = new CancellationTokenSource();
        try
        {
            var run = await _service.CleanAsync(selected, new Progress<CleanProgress>(OnProgress), _cts.Token);
            ShowResult(run, selected);
        }
        catch (Exception ex)
        {
            Status = "Cleanup failed: " + ex.Message;
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            ProgressText = string.Empty;
            _scanValid = false; // the old estimate no longer applies
            IsBusy = false;
        }
    }

    private void ShowResult(CleanupRun run, List<CacheCategory> selected)
    {
        var t = CleanupTotals.From(run.Results);
        ReportLines.Clear();
        if (run.Cancelled) ReportLines.Add("Cleanup was cancelled. The numbers below only cover what was done before cancelling.");
        ReportLines.Add("Files deleted: " + t.Files);
        ReportLines.Add("Space freed (sum of the sizes of the deleted files): " + SizeText.Format(t.Bytes));
        ReportLines.Add("Empty folders removed: " + t.FoldersRemoved);
        ReportLines.Add("Items skipped: " + t.SkippedItems);
        foreach (var kv in t.SkippedByReason.OrderByDescending(k => k.Value))
        {
            ReportLines.Add("   - " + SkipReasonText.Describe(kv.Key) + ": " + kv.Value);
        }

        foreach (var r in run.Results)
        {
            var line = r.CategoryName + ": " + r.Tally.Files + " files, " + SizeText.Format(r.Tally.Bytes);
            if (r.Tally.SkippedItems > 0) line += ", " + r.Tally.SkippedItems + " skipped";
            if (r.Message.Length > 0) line += " - " + r.Message;
            ReportLines.Add(line);
        }

        var details = run.Results.SelectMany(r => r.Tally.Details).Take(CleanupTally.MaxDetails).ToList();
        if (details.Count > 0)
        {
            ReportLines.Add("Error details (first " + details.Count + "):");
            foreach (var d in details) ReportLines.Add("   " + d);
        }

        var entry = new CleanupHistoryEntry(
            DateTime.UtcNow,
            run.Results.Select(r => r.CategoryName).ToArray(),
            t.Files, t.Bytes, t.SkippedItems, run.Cancelled);
        if (!_service.AppendHistory(entry)) ReportLines.Add("The history file could not be saved.");
        RefreshHistory(_service.LoadHistory());

        ShowReport = true;
        ScanSummary = string.Empty;
        foreach (var c in Categories) c.ResultText = "Not scanned yet.";
        Status = run.Cancelled
            ? "Cleanup cancelled."
            : t.Files > 0
                ? "Cleanup finished: " + t.Files + " files deleted. Scan again to refresh estimates."
                : "Cleanup finished, but no file could be deleted. See the report.";
    }

    private void RefreshHistory(IEnumerable<CleanupHistoryEntry> entries)
    {
        HistoryLines.Clear();
        foreach (var e in entries.Take(10))
        {
            var cats = e.Categories == null ? string.Empty : string.Join(", ", e.Categories);
            var line = e.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm") + " - " + e.FilesDeleted + " files deleted, "
                + SizeText.Format(e.BytesFreed) + " freed, " + e.FilesSkipped + " skipped";
            if (e.Cancelled) line += " (cancelled)";
            if (cats.Length > 0) line += " - " + cats;
            HistoryLines.Add(line);
        }
        HistoryEmpty = HistoryLines.Count == 0;
    }
}
