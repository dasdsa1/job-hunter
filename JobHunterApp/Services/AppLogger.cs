using System.Text;
using JobHunterApp.Models;

namespace JobHunterApp.Services;

/// <summary>
/// Simple synchronous file logger. Thread-safe via lock.
/// Log file: %LocalAppData%\JobHunter\app.log
/// </summary>
public static class AppLogger
{
    private static readonly string LogPath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JobHunter", "app.log");

    private static readonly object _lock = new();

    public static void Info(string msg)  => Write("INFO ", msg);
    public static void Warn(string msg)  => Write("WARN ", msg);
    public static void Error(string msg) => Write("ERROR", msg);

    public static void Exception(string context, Exception ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[ERROR] {context}");
        AppendException(sb, ex, 0);
        Write("EXCEP", sb.ToString());
    }

    private static void AppendException(StringBuilder sb, Exception? ex, int depth)
    {
        if (ex is null) return;
        var indent = new string(' ', depth * 2);
        sb.AppendLine($"{indent}Type   : {ex.GetType().FullName}");
        sb.AppendLine($"{indent}Message: {ex.Message}");
        if (!string.IsNullOrWhiteSpace(ex.StackTrace))
        {
            foreach (var line in ex.StackTrace.Split('\n'))
                sb.AppendLine($"{indent}  {line.TrimEnd()}");
        }
        if (ex.InnerException is not null)
        {
            sb.AppendLine($"{indent}--- Inner Exception ---");
            AppendException(sb, ex.InnerException, depth + 1);
        }
    }

    private static void Write(string level, string msg)
    {
        try
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {msg}";
            lock (_lock)
                File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
        }
        catch { /* never let logging crash the app */ }
    }
}
