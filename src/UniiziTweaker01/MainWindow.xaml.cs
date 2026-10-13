using System.Windows;
using UniiziTweaker01.ViewModels;

namespace UniiziTweaker01;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += (_, _) => _vm.Start();
        StateChanged += OnStateChanged;
        Closing += (_, _) => _vm.Shutdown();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        // A borderless maximized window overhangs the screen edge slightly; compensate.
        RootGrid.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        MaxButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        _vm.SetMinimized(WindowState == WindowState.Minimized);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
