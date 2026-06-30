using System.Text.Json;
using JobHunterApp.Models;

namespace JobHunterApp.Services;

/// <summary>
/// Config loader for the headless worker.
/// Priority: GEMINI_API_KEY env var > config.json (plaintext, no DPAPI in container).
/// </summary>
public static class WorkerConfigService
{
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static AppConfig LoadAppConfig()
    {
        var config = new AppConfig();

        if (File.Exists(AppPaths.ConfigFile))
        {
            try
            {
                config = JsonSerializer.Deserialize<AppConfig>(
                    File.ReadAllText(AppPaths.ConfigFile), Opts) ?? new();
            }
            catch (Exception ex)
            {
                AppLogger.Exception("WorkerConfigService.LoadAppConfig", ex);
            }
        }

        // Env var overrides stored key (avoids DPAPI issue in Linux containers)
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrEmpty(apiKey))
            config.ApiKey = apiKey;

        // Adzuna credentials via env (optional)
        var adzId  = Environment.GetEnvironmentVariable("ADZUNA_APP_ID");
        var adzKey = Environment.GetEnvironmentVariable("ADZUNA_APP_KEY");
        var adzCty = Environment.GetEnvironmentVariable("ADZUNA_COUNTRY");
        if (!string.IsNullOrEmpty(adzId))  config.AdzunaAppId   = adzId;
        if (!string.IsNullOrEmpty(adzKey)) config.AdzunaAppKey  = adzKey;
        if (!string.IsNullOrEmpty(adzCty)) config.AdzunaCountry = adzCty;

        return config;
    }

    public static SearchConfig LoadSearchConfig()
    {
        if (File.Exists(AppPaths.SearchConfigFile))
        {
            try
            {
                return JsonSerializer.Deserialize<SearchConfig>(
                    File.ReadAllText(AppPaths.SearchConfigFile), Opts) ?? new();
            }
            catch (Exception ex)
            {
                AppLogger.Exception("WorkerConfigService.LoadSearchConfig", ex);
            }
        }

        // Env var overrides for quick CLI usage.
        // SEARCH_SITES: comma-separated, e.g. "remotive,remoteok,arbeitnow,adzuna,linkedin,indeed".
        // Defaults to the headless API sources so a bare Worker run needs no browser/login.
        var sitesEnv = Environment.GetEnvironmentVariable("SEARCH_SITES");
        var sites = string.IsNullOrWhiteSpace(sitesEnv)
            ? new List<string> { "remotive", "remoteok", "arbeitnow" }
            : sitesEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                      .Select(s => s.ToLowerInvariant()).ToList();

        return new SearchConfig
        {
            JobTitle       = Environment.GetEnvironmentVariable("SEARCH_TITLE")    ?? "",
            Location       = Environment.GetEnvironmentVariable("SEARCH_LOCATION") ?? "Remote",
            Keywords       = Environment.GetEnvironmentVariable("SEARCH_KEYWORDS") ?? "",
            Sites          = sites,
            MinScore       = int.TryParse(Environment.GetEnvironmentVariable("SEARCH_MIN_SCORE"), out var s) ? s : 6,
            MaxJobsPerSite = int.TryParse(Environment.GetEnvironmentVariable("SEARCH_MAX_JOBS"), out var m) ? m : 20,
            SkipAppliedJobs = true,
        };
    }
}
