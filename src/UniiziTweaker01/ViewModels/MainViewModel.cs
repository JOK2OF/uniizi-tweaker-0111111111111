using System.Collections.ObjectModel;
using UniiziTweaker01.Infrastructure;
using UniiziTweaker01.Services;

namespace UniiziTweaker01.ViewModels;

public sealed record NavItem(string Title, string Glyph, object Page, bool IsImplemented)
{
    public bool IsPending => !IsImplemented;
}

public sealed class MainViewModel : ObservableObject
{
    private NavItem? _selected;
    private object? _currentPage;

    public MainViewModel()
    {
        Dashboard = new DashboardViewModel(new SystemMonitorService());
        Resolution = new ResolutionViewModel(new DisplayService());

        Items = new ObservableCollection<NavItem>
        {
            new("Dashboard", "\uE80F", Dashboard, true),
            new("Quick Boost", "\uE945", Pending("Quick Boost", "One-click optimization preview with per-action configuration and a results report.", "Step 6 (after the Cache Cleaner and Gaming Optimizer exist)"), false),
            new("Gaming Optimization", "\uE7FC", Pending("Gaming Optimization", "Windows gaming settings shortcuts and the Balanced / Gaming / Max Performance / Custom profiles.", "Step 6"), false),
            new("Memory & Processes", "\uE950", Pending("Memory & Processes", "Memory details, process list with confirmation before closing normal user applications.", "Step 5"), false),
            new("Cache Cleaner", "\uE74D", Pending("Cache Cleaner", "Scan-then-clean with per-category selection, safety rules, history and exact byte reports.", "Step 4"), false),
            new("Resolution Manager", "\uE7F4", Resolution, true),
            new("Windows Tweaks", "\uE713", Pending("Windows Tweaks", "Documented, reversible tweaks with backups and an undo button for each.", "Step 6"), false),
            new("Startup Manager", "\uE768", Pending("Startup Manager", "List, enable and disable startup entries with confirmation.", "Step 7"), false),
            new("Network Tools", "\uE774", Pending("Network Tools", "Adapter info, DNS lookup, latency and packet-loss tests.", "Step 8"), false),
            new("Game Profiles", "\uE7FC", Pending("Game Profiles", "Per-game profiles, launch and session monitoring.", "Step 9"), false),
            new("System Monitor", "\uE9D9", Pending("System Monitor", "Full charts, CSV export and before/after comparisons.", "After step 9"), false),
            new("Restore & Backups", "\uE777", Pending("Restore & Backups", "Operation history, backups and undo.", "Step 10"), false),
            new("Settings", "\uE713", Pending("Settings", "Theme, reduced motion, refresh rate, language and confirmations.", "Step 10"), false),
            new("About UNIIZI", "\uE946", Pending("About UNIIZI", "Version, licences and build information.", "Step 10"), false),
        };

        Selected = Items[0];
        WindowsStatus = SystemInfoProvider.GetWindowsVersion();
        OpenSettingsCommand = new RelayCommand(_ => Selected = Items.First(i => i.Title == "Settings"));
    }

    public DashboardViewModel Dashboard { get; }
    public ResolutionViewModel Resolution { get; }
    public ObservableCollection<NavItem> Items { get; }
    public string WindowsStatus { get; }
    public string ProfileText => "Profile: none active";
    public RelayCommand OpenSettingsCommand { get; }

    public NavItem? Selected
    {
        get => _selected;
        set
        {
            if (value == null) return; // keep a page selected at all times
            if (Set(ref _selected, value)) CurrentPage = value.Page;
        }
    }

    public object? CurrentPage { get => _currentPage; private set => Set(ref _currentPage, value); }

    public void Start()
    {
        Dashboard.Start();
        Resolution.Initialize();
    }

    public void SetMinimized(bool minimized) => Dashboard.Suspended = minimized;

    public void Shutdown()
    {
        Dashboard.Stop();
        Resolution.Shutdown();
    }

    private static PendingModuleViewModel Pending(string title, string description, string step) =>
        new(title, description, step);
}
