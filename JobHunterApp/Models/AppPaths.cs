namespace JobHunterApp.Models;

public static class AppPaths
{
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JobHunter");

    public static string ConfigFile             => Path.Combine(Root, "config.json");
    public static string SearchHistory          => Path.Combine(Root, "search_history.json");
    public static string AppliedJobsFile        => Path.Combine(Root, "applied_jobs.json");
    public static string ReportsDir             => Path.Combine(Root, "reports");
    public static string BrowserProfileChromium => Path.Combine(Root, "browser-profile-chromium");
    public static string BrowserProfileFirefox  => Path.Combine(Root, "browser-profile-firefox");
    public static string TailoredCvsDir         => Path.Combine(Root, "tailored-cvs");
    public static string CoverLettersDir        => Path.Combine(Root, "cover-letters");
    public static string ResumeCacheFile        => Path.Combine(Root, "resume_cache.json");
    public static string DeviceDbFile            => Path.Combine(Root, "jobhunter.db");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(ReportsDir);
        Directory.CreateDirectory(BrowserProfileChromium);
        Directory.CreateDirectory(BrowserProfileFirefox);
        Directory.CreateDirectory(TailoredCvsDir);
        Directory.CreateDirectory(CoverLettersDir);
    }
}
