using System.Text;

namespace JobHunterApp.Services;

/// <summary>
/// Worker version of AppLogger — writes to stdout (container-friendly) and a log file.
/// </summary>
public static class AppLogger
{
    private static readonly string LogPath = Path.Combine(JobHunterApp.Models.AppPaths.Root, "worker.log");
    private static readonly object _lock = new();

    public static void Info(string msg)  => Write("INFO ", msg);
    public static void Warn(string msg)  => Write("WARN ", msg);
    public static void Error(string msg) => Write("ERROR", msg);

    public static void Exception(string context, Exception ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[ERROR] {context}");
        Append(sb, ex, 0);
        Write("EXCEP", sb.ToString());
    }

    private static void Append(StringBuilder sb, Exception? ex, int depth)
    {
        if (ex is null) return;
        var indent = new string(' ', depth * 2);
        sb.AppendLine($"{indent}{ex.GetType().Name}: {ex.Message}");
        if (ex.InnerException is not null) Append(sb, ex.InnerException, depth + 1);
    }

    private static void Write(string level, string msg)
    {
        var line = $"[{DateTime.UtcNow:HH:mm:ss.fff}] [{level}] {msg}";
        lock (_lock)
        {
            Console.WriteLine(line);
            try { File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8); }
            catch { }
        }
    }
}
