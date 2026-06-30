using Microsoft.Playwright;
using JobHunterApp.Models;
using JobHunterApp.Services;
using JobHunterApp.Services.Scrapers;
using JobHunterApp.Services.Sources;

// ── Bootstrap ─────────────────────────────────────────────────────────────────
AppPaths.EnsureDirectories();
AppLogger.Info("Job Hunter Worker starting");

var appConfig    = WorkerConfigService.LoadAppConfig();
var searchConfig = WorkerConfigService.LoadSearchConfig();

if (string.IsNullOrEmpty(appConfig.ApiKey))
{
    AppLogger.Error("No API key. Set GEMINI_API_KEY env var or put plaintext key in config.json.");
    return 1;
}

if (string.IsNullOrEmpty(searchConfig.JobTitle))
{
    AppLogger.Error("No job title. Set SEARCH_TITLE env var or create search.json.");
    return 1;
}

AppLogger.Info($"Searching: \"{searchConfig.JobTitle}\" in \"{searchConfig.Location}\"");
AppLogger.Info($"Sites: {string.Join(", ", searchConfig.Sites)}  MaxPerSite: {searchConfig.MaxJobsPerSite}");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); AppLogger.Info("Cancelled by user."); };

var log = new Progress<string>(AppLogger.Info);
var allJobs = new List<JobListing>();

// ── API sources (headless, no browser, fully automated) ─────────────────────────
if (ApiJobSources.AnyEnabled(searchConfig, appConfig))
{
    AppLogger.Info("Fetching headless API sources…");
    try
    {
        var apiJobs = await ApiJobSources.FetchAllAsync(searchConfig, appConfig, log, cts.Token);
        AppLogger.Info($"API sources: {apiJobs.Count} jobs found");
        allJobs.AddRange(apiJobs);
    }
    catch (OperationCanceledException) { AppLogger.Info("API fetch cancelled."); }
}

// ── Browser scrapers (only launch Playwright if a browser site is selected) ─────
var needBrowser = searchConfig.Sites.Contains("linkedin", StringComparer.OrdinalIgnoreCase)
               || searchConfig.Sites.Contains("indeed",   StringComparer.OrdinalIgnoreCase);
IBrowserContext? context = null;

if (needBrowser)
{
    var playwright = await Playwright.CreateAsync();
    var headless   = Environment.GetEnvironmentVariable("JOBHUNTER_HEADLESS") == "true";
    AppLogger.Info($"Browser mode: {(headless ? "headless" : "headed")} Playwright Chromium");

    // Always use bundled Playwright Chromium in Docker (no system Chrome/Edge needed)
    context = await playwright.Chromium.LaunchPersistentContextAsync(
        AppPaths.BrowserProfileChromium,
        new BrowserTypeLaunchPersistentContextOptions
        {
            Headless          = headless,
            Args              = ["--disable-blink-features=AutomationControlled",
                                 "--no-sandbox",           // required in Docker
                                 "--disable-dev-shm-usage"], // required in Docker
            ViewportSize      = new ViewportSize { Width = 1280, Height = 900 },
            IgnoreHTTPSErrors = true,
        });

    try
    {
        if (searchConfig.Sites.Contains("linkedin", StringComparer.OrdinalIgnoreCase))
        {
            AppLogger.Info("Scraping LinkedIn…");
            var jobs = await LinkedInScraper.ScrapeAsync(context, searchConfig, log, cts.Token);
            AppLogger.Info($"LinkedIn: {jobs.Count} jobs found");
            allJobs.AddRange(jobs);
        }

        if (searchConfig.Sites.Contains("indeed", StringComparer.OrdinalIgnoreCase))
        {
            AppLogger.Info("Scraping Indeed…");
            var jobs = await IndeedScraper.ScrapeAsync(context, searchConfig, log, cts.Token);
            AppLogger.Info($"Indeed: {jobs.Count} jobs found");
            allJobs.AddRange(jobs);
        }
    }
    catch (OperationCanceledException)
    {
        AppLogger.Info("Scraping cancelled.");
    }
}

AppLogger.Info($"Total jobs: {allJobs.Count}");

if (allJobs.Count == 0)
{
    AppLogger.Warn("No jobs found. Check search config or login state.");
    if (context is not null) await context.CloseAsync();
    return 0;
}

// ── Filter applied ────────────────────────────────────────────────────────────
var appliedStore = AppliedJobsService.Load();
if (searchConfig.SkipAppliedJobs)
{
    var before = allJobs.Count;
    allJobs = allJobs.Where(j => !AppliedJobsService.IsApplied(j.Id, appliedStore)).ToList();
    AppLogger.Info($"Skipped {before - allJobs.Count} already-applied jobs. {allJobs.Count} remaining.");
}

// ── Score with Gemini ─────────────────────────────────────────────────────────
var resume = "";
if (!string.IsNullOrEmpty(appConfig.Cv?.Path) && File.Exists(appConfig.Cv.Path))
{
    try { resume = await ResumeParserService.ParseAsync(appConfig.Cv.Path); }
    catch (Exception ex) { AppLogger.Exception("ParseResume", ex); }
}
if (string.IsNullOrEmpty(resume))
    AppLogger.Warn("CV not found or empty — scores will be lower quality.");

AppLogger.Info("Scoring jobs with Gemini…");
var rateLimiter = new RateLimiter(appConfig.GeminiRpm);
var gemini      = new GeminiService(appConfig.ApiKey, appConfig.GeminiModel, rateLimiter);
var scores  = await gemini.MatchJobsAsync(allJobs, resume);

var matches = allJobs
    .Where(j => scores.TryGetValue(j.Id, out var r) && r.Score >= searchConfig.MinScore)
    .Select(j => new JobMatch { Job = j, Match = scores[j.Id] })
    .OrderByDescending(m => m.Match.Score)
    .ToList();

AppLogger.Info($"Matched {matches.Count}/{allJobs.Count} jobs at score >= {searchConfig.MinScore}");

// ── Auto-generate cover letters for matches (fully automated, no interaction) ────
if (matches.Count > 0 && !string.IsNullOrEmpty(resume))
{
    AppLogger.Info($"Generating cover letters for {matches.Count} match(es)…");
    foreach (var m in matches)
    {
        if (cts.Token.IsCancellationRequested) break;
        try
        {
            m.CoverLetter = await gemini.GenerateCoverLetterAsync(
                m.Job, m.Match, resume, Array.Empty<string>());
            m.CoverLetterPath = CoverLetterService.SaveAsDocx(m.CoverLetter, m.Job);
            AppLogger.Info($"  ✔ {m.Job.Title} @ {m.Job.Company} → {m.CoverLetterPath}");
        }
        catch (Exception ex)
        {
            AppLogger.Exception($"CoverLetter {m.Job.Id}", ex);
        }
    }
}

// ── Report ────────────────────────────────────────────────────────────────────
var reportPath = ReporterService.GenerateReport(searchConfig, allJobs.Count, matches.Count, matches);
AppLogger.Info($"Report saved: {reportPath}");

// ── Cleanup ───────────────────────────────────────────────────────────────────
if (context is not null) await context.CloseAsync();
AppLogger.Info("Done.");
return 0;
