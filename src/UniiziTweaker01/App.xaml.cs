using System.Windows;
using System.Windows.Threading;
using UniiziTweaker01.Infrastructure;

namespace UniiziTweaker01;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        bool smoke = e.Args.Contains("--smoke-test");

        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("Unhandled UI exception", args.Exception);
            if (smoke) Environment.Exit(1);
            MessageBox.Show(
                "An unexpected error occurred. Details were written to:\n" + AppLog.LogDirectory + "\n\n" + args.Exception.Message,
                "UNIIZI TWEAKER 01", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("Unhandled exception", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        AppLog.Info("Application started" + (smoke ? " (smoke test)" : ""));
        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        if (smoke)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            timer.Tick += (_, _) => { timer.Stop(); Shutdown(0); };
            timer.Start();
        }
    }
}
