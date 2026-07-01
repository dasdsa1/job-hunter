using System.Text.Json;
using JobHunterApp.Models;

namespace JobHunterApp.Services;

/// <summary>
/// Caches parsed resume text on disk, keyed by CV file path + last-write time.
/// Survives a crash mid-run (so the next run doesn't need to re-parse), and avoids
/// re-parsing the same CV file across consecutive runs.
/// </summary>
public static class ResumeCacheService
{
    private class Entry
    {
        public string  Path     { get; set; } = "";
        public string  MTime    { get; set; } = "";
        public string  Text     { get; set; } = "";
        public string? Profile  { get; set; }
    }

    public static string? TryGet(string cvPath) => Load(cvPath)?.Text;

    /// <summary>Condensed skills/experience summary extracted once per CV, so scoring batches
    /// send this instead of the full resume text on every LLM call.</summary>
    public static string? TryGetProfile(string cvPath) => Load(cvPath)?.Profile;

    private static Entry? Load(string cvPath)
    {
        try
        {
            if (!File.Exists(AppPaths.ResumeCacheFile)) return null;
            var entry = JsonSerializer.Deserialize<Entry>(File.ReadAllText(AppPaths.ResumeCacheFile));
            if (entry is null || entry.Path != cvPath) return null;
            var mtime = File.GetLastWriteTimeUtc(cvPath).ToString("o");
            return entry.MTime == mtime ? entry : null;
        }
        catch (Exception ex)
        {
            AppLogger.Exception("ResumeCacheService.Load", ex);
            return null;
        }
    }

    public static void Save(string cvPath, string text)
    {
        try
        {
            var entry = new Entry
            {
                Path  = cvPath,
                MTime = File.GetLastWriteTimeUtc(cvPath).ToString("o"),
                Text  = text
            };
            File.WriteAllText(AppPaths.ResumeCacheFile, JsonSerializer.Serialize(entry));
        }
        catch (Exception ex)
        {
            AppLogger.Exception("ResumeCacheService.Save", ex);
        }
    }

    public static void SaveProfile(string cvPath, string text, string profile)
    {
        try
        {
            var entry = new Entry
            {
                Path    = cvPath,
                MTime   = File.GetLastWriteTimeUtc(cvPath).ToString("o"),
                Text    = text,
                Profile = profile
            };
            File.WriteAllText(AppPaths.ResumeCacheFile, JsonSerializer.Serialize(entry));
        }
        catch (Exception ex)
        {
            AppLogger.Exception("ResumeCacheService.SaveProfile", ex);
        }
    }
}
