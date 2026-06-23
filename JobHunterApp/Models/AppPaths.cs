namespace JobHunterApp.Models;

public static class AppPaths
{
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JobHunter");

    public static string ConfigFile      => Path.Combine(Root, "config.json");
    public static string SearchHistory   => Path.Combine(Root, "search_history.json");
    public static string ReportsDir      => Path.Combine(Root, "reports");
    public static string BrowserProfile  => Path.Combine(Root, "browser-profile");
    public static string TailoredCvsDir  => Path.Combine(Root, "tailored-cvs");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(ReportsDir);
        Directory.CreateDirectory(BrowserProfile);
        Directory.CreateDirectory(TailoredCvsDir);
    }
}
