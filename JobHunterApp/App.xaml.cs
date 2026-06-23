using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using JobHunterApp.Models;
using JobHunterApp.Services;

namespace JobHunterApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppPaths.EnsureDirectories();
        AppLogger.Info("=== App started ===");

        DispatcherUnhandledException   += OnDispatcherException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Exception("DispatcherUnhandledException", e.Exception);

        var detail = BuildDetail(e.Exception);
        MessageBox.Show(detail,
            "Job Hunter — Unhandled Error",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        AppLogger.Exception("UnobservedTaskException", e.Exception);

        var detail = BuildDetail(e.Exception.InnerException ?? e.Exception);
        Current.Dispatcher.Invoke(() =>
            MessageBox.Show(detail,
                "Job Hunter — Task Error",
                MessageBoxButton.OK, MessageBoxImage.Warning));
    }

    private static string BuildDetail(Exception ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine(ex.Message);
        sb.AppendLine();
        sb.AppendLine($"Type: {ex.GetType().Name}");

        if (ex.InnerException is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"Inner: {ex.InnerException.Message}");
            sb.AppendLine($"       {ex.InnerException.GetType().Name}");
        }

        if (!string.IsNullOrWhiteSpace(ex.StackTrace))
        {
            sb.AppendLine();
            // Show top 5 stack frames so the dialog stays readable
            var frames = ex.StackTrace.Split('\n')
                           .Where(l => l.Trim().StartsWith("at "))
                           .Take(5);
            foreach (var f in frames)
                sb.AppendLine(f.TrimEnd());
        }

        sb.AppendLine();
        sb.AppendLine($"Full log: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JobHunter", "app.log")}");
        return sb.ToString();
    }
}
