using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using JobHunterApp.Models;

namespace JobHunterApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppPaths.EnsureDirectories();

        // Catch exceptions thrown on the UI thread (e.g. XAML event handlers)
        DispatcherUnhandledException += OnDispatcherException;

        // Catch unobserved Task exceptions (fire-and-forget faults)
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\n{e.Exception.GetType().Name}",
            "Job Hunter — Unhandled Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true; // prevent crash
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved(); // prevent process termination
        Current.Dispatcher.Invoke(() =>
            MessageBox.Show(
                $"Background task error:\n\n{e.Exception.InnerException?.Message ?? e.Exception.Message}",
                "Job Hunter — Task Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning));
    }
}
