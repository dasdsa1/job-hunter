namespace JobHunterApp.Models;

/// <summary>
/// Worker version of AppPaths — reads data dir from JOBHUNTER_DATA_DIR env var.
/// Falls back to %LocalAppData%\JobHunter so local dev works without env vars.
/// </summary>
public static class AppPaths
{
    public static readonly string Root =
        Environment.GetEnvironmentVariable("JOBHUNTER_DATA_DIR")
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JobHunter");

    public static string ConfigFile             => Path.Combine(Root, "config.json");
    public static string SearchConfigFile       => Path.Combine(Root, "search.json");
    public static string SearchHistory          => Path.Combine(Root, "search_history.json");
    public static string AppliedJobsFile        => Path.Combine(Root, "applied_jobs.json");
    public static string ResumeCacheFile        => Path.Combine(Root, "resume_cache.json");
    public static string ReportsDir             => Path.Combine(Root, "reports");
    public static string BrowserProfileChromium => Path.Combine(Root, "browser-profile-chromium");
    public static string BrowserProfileFirefox  => Path.Combine(Root, "browser-profile-firefox");
    public static string TailoredCvsDir         => Path.Combine(Root, "tailored-cvs");
    public static string CoverLettersDir        => Path.Combine(Root, "cover-letters");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(ReportsDir);
        Directory.CreateDirectory(BrowserProfileChromium);
        Directory.CreateDirectory(TailoredCvsDir);
        Directory.CreateDirectory(CoverLettersDir);
    }
}
