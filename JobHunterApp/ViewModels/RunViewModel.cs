using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JobHunterApp.Models;
using JobHunterApp.Services;
using JobHunterApp.Services.Applicators;
using JobHunterApp.Services.Scrapers;
using Microsoft.Playwright;

namespace JobHunterApp.ViewModels;

public enum RunStep { Idle, Scraping, Matching, SelectingJobs, Applying, Done }

// ── Interaction requests (bot pauses and awaits UI response) ─────────────────

public abstract class InteractionRequest { }

public class CvChoiceRequest(string jobTitle, string company) : InteractionRequest
{
    public string JobTitle { get; } = jobTitle;
    public string Company  { get; } = company;
    public TaskCompletionSource<bool> Tcs { get; } = new(); // true = tailor
}

public class LetterChoiceRequest(List<FileEntry> available) : InteractionRequest
{
    public List<FileEntry> Available { get; } = available;
    public TaskCompletionSource<List<FileEntry>> Tcs { get; } = new();
}

public class ReviewRequest(string message) : InteractionRequest
{
    public string Message { get; } = message;
    public TaskCompletionSource<bool> Tcs { get; } = new();
}

// ────────────────────────────────────────────────────────────────────────────

public partial class RunViewModel : ObservableObject
{
    [ObservableProperty] private RunStep             _currentStep       = RunStep.Idle;
    [ObservableProperty] private string              _statusText        = "Ready";
    [ObservableProperty] private bool                _isRunning;
    [ObservableProperty] private InteractionRequest? _pendingInteraction;
    [ObservableProperty] private string              _coverLetterPreview = "";
    [ObservableProperty] private string?             _reportPath;

    public ObservableCollection<string>    Log     { get; } = [];
    public ObservableCollection<JobMatch>  Jobs    { get; } = [];

    // ── Interaction responses ────────────────────────────────────────────────

    [RelayCommand]
    private void UseDefaultCv()
    {
        if (PendingInteraction is CvChoiceRequest r) r.Tcs.TrySetResult(false);
    }

    [RelayCommand]
    private void UseTailoredCv()
    {
        if (PendingInteraction is CvChoiceRequest r) r.Tcs.TrySetResult(true);
    }

    [RelayCommand]
    private void ConfirmLetters()
    {
        if (PendingInteraction is LetterChoiceRequest r)
            r.Tcs.TrySetResult(r.Available.Where(l => l.IsSelected).ToList());
    }

    [RelayCommand]
    private void ProceedWithApplication()
    {
        if (PendingInteraction is ReviewRequest r) r.Tcs.TrySetResult(true);
    }

    [RelayCommand]
    private void SkipApplication()
    {
        if (PendingInteraction is ReviewRequest r) r.Tcs.TrySetResult(false);
    }

    [RelayCommand]
    private void ApplyToSelected()
    {
        if (_applySelectionTcs is not null)
            _applySelectionTcs.TrySetResult(Jobs.Where(j => j.IsSelected).ToList());
    }

    [RelayCommand]
    private void OpenReport()
    {
        if (ReportPath is not null && File.Exists(ReportPath))
            Process.Start(new ProcessStartInfo(ReportPath) { UseShellExecute = true });
    }

    private TaskCompletionSource<List<JobMatch>>? _applySelectionTcs;

    // ── Main run method ──────────────────────────────────────────────────────

    public async Task RunAsync(SearchConfig config)
    {
        try
        {
            await RunCoreAsync(config);
        }
        catch (Exception ex)
        {
            AddLog($"❌  Unexpected error: {ex.Message}");
            AddLog($"    {ex.GetType().Name}");
            if (ex.InnerException is not null)
                AddLog($"    Inner: {ex.InnerException.Message}");
            Finish();
        }
    }

    private async Task RunCoreAsync(SearchConfig config)
    {
        IsRunning   = true;
        CurrentStep = RunStep.Scraping;
        StatusText  = "Scraping jobs…";
        Log.Clear();
        Jobs.Clear();
        ReportPath  = null;

        var appConfig = FileConfigService.Load();
        if (string.IsNullOrWhiteSpace(appConfig.ApiKey))
        {
            AddLog("❌  No API key configured. Go to Setup and save your Gemini API key.");
            Finish();
            return;
        }
        if (appConfig.Cv is null || !File.Exists(appConfig.Cv.Path))
        {
            AddLog("❌  No CV file configured or file not found. Go to Setup to select your CV.");
            Finish();
            return;
        }

        var rateLimiter = new RateLimiter(appConfig.GeminiRpm);
        var gemini      = new GeminiService(appConfig.ApiKey, appConfig.GeminiModel, rateLimiter);
        var progress    = new Progress<string>(AddLog);

        // ── 1. Parse CV ──────────────────────────────────────────────────────
        string resume;
        try
        {
            resume = await ResumeParserService.ParseAsync(appConfig.Cv.Path);
            AddLog($"✔  CV loaded ({resume.Length:N0} chars)");
        }
        catch (Exception ex)
        {
            AddLog($"❌  Failed to read CV: {ex.Message}");
            Finish();
            return;
        }

        // Pre-parse all letter texts
        var letterTexts = new Dictionary<string, string>();
        foreach (var letter in appConfig.Letters)
        {
            try { letterTexts[letter.Key] = await ResumeParserService.ParseAsync(letter.Path); }
            catch { letterTexts[letter.Key] = ""; }
        }

        // ── 2. Scrape ────────────────────────────────────────────────────────
        IBrowserContext? browser = null;
        try
        {
            browser = await BrowserService.CreateContextAsync(appConfig);
        }
        catch (Exception ex)
        {
            AddLog($"❌  Failed to launch browser: {ex.Message}");
            AddLog("    Try running: dotnet tool install --global Microsoft.Playwright.CLI && playwright install chromium");
            Finish();
            return;
        }

        var allJobs = new List<JobListing>();
        try
        {
            if (config.Sites.Contains("linkedin"))
                allJobs.AddRange(await LinkedInScraper.ScrapeAsync(browser, config, progress));
            if (config.Sites.Contains("indeed"))
                allJobs.AddRange(await IndeedScraper.ScrapeAsync(browser, config, progress));
        }
        catch (Exception ex) { AddLog($"Scraping error: {ex.Message}"); }

        if (allJobs.Count == 0)
        {
            AddLog("No jobs found. Try broader search terms or different sites.");
            await browser.CloseAsync();
            Finish();
            return;
        }

        // ── 3. Match ─────────────────────────────────────────────────────────
        CurrentStep = RunStep.Matching;
        StatusText  = "Scoring jobs with AI…";
        AddLog($"Scoring {allJobs.Count} job(s)…");

        Dictionary<string, MatchResult> scoreMap;
        try
        {
            scoreMap = await gemini.MatchJobsAsync(allJobs, resume);
            AddLog($"✔  Scored {scoreMap.Count} job(s)");
        }
        catch (Exception ex)
        {
            AddLog($"Matching failed: {ex.Message}");
            scoreMap = [];
        }

        var matches = allJobs
            .Select(j => new JobMatch
            {
                Job   = j,
                Match = scoreMap.TryGetValue(j.Id, out var m) ? m : new MatchResult { Score = 0, Summary = "No score" }
            })
            .Where(m => m.Match.Score >= config.MinScore)
            .OrderByDescending(m => m.Match.Score)
            .ToList();

        if (matches.Count == 0)
        {
            AddLog($"No jobs scored ≥ {config.MinScore}.");
            ReportPath = ReporterService.GenerateReport(config, allJobs.Count, 0, []);
            await browser.CloseAsync();
            Finish();
            return;
        }

        // ── 4. Show results, wait for selection ──────────────────────────────
        CurrentStep = RunStep.SelectingJobs;
        StatusText  = $"Found {matches.Count} matching job(s) — select which to apply to";

        foreach (var m in matches)
        {
            m.IsSelected = m.Match.Score >= 8;
            Application.Current.Dispatcher.Invoke(() => Jobs.Add(m));
        }

        _applySelectionTcs = new TaskCompletionSource<List<JobMatch>>();
        var selected = await _applySelectionTcs.Task;
        _applySelectionTcs = null;

        if (selected.Count == 0)
        {
            AddLog("No jobs selected.");
            ReportPath = ReporterService.GenerateReport(config, allJobs.Count, matches.Count, matches);
            await browser.CloseAsync();
            Finish();
            return;
        }

        // ── 5. Apply ─────────────────────────────────────────────────────────
        CurrentStep = RunStep.Applying;
        StatusText  = "Applying…";

        foreach (var jm in selected)
        {
            AddLog($"\n▶  {jm.Job.Title} @ {jm.Job.Company}  [{jm.Match.Score}/10]");

            // CV choice
            var cvReq = new CvChoiceRequest(jm.Job.Title, jm.Job.Company);
            await ShowInteractionAsync(cvReq);
            var tailor = await cvReq.Tcs.Task;

            var activeResume = resume;
            if (tailor)
            {
                AddLog("Tailoring CV…");
                try
                {
                    activeResume = await gemini.TailorCvAsync(resume, jm.Job, jm.Match);
                    var docxPath = CvTailorService.SaveAsDocx(activeResume, jm.Job);
                    AddLog($"Tailored CV saved → {docxPath}");
                }
                catch (Exception ex) { AddLog($"CV tailor failed: {ex.Message}"); }
            }

            // Letter choice
            List<FileEntry> selectedLetters = [];
            if (appConfig.Letters.Count > 0)
            {
                foreach (var l in appConfig.Letters) l.IsSelected = false;
                var letterReq = new LetterChoiceRequest([.. appConfig.Letters]);
                await ShowInteractionAsync(letterReq);
                selectedLetters = await letterReq.Tcs.Task;
            }

            // Cover letter
            AddLog("Generating cover letter…");
            CoverLetterPreview = "";
            try
            {
                var snippets = selectedLetters.Select(l => letterTexts.TryGetValue(l.Key, out var t) ? t : "");
                jm.CoverLetter = await gemini.GenerateCoverLetterAsync(
                    jm.Job, jm.Match, activeResume, snippets,
                    chunk => Application.Current.Dispatcher.Invoke(
                        () => CoverLetterPreview += chunk));
                AddLog($"✔  Cover letter ready ({jm.CoverLetter.Split(' ').Length} words)");
            }
            catch (Exception ex) { AddLog($"Cover letter failed: {ex.Message}"); }

            // Confirm
            var confirmReq = new ReviewRequest("Review the cover letter above, then proceed or skip this application.");
            await ShowInteractionAsync(confirmReq);
            if (!await confirmReq.Tcs.Task)
            {
                AddLog("Skipped.");
                continue;
            }

            // Apply
            bool success;
            Func<Task> waitReview = async () =>
            {
                var reviewReq = new ReviewRequest("Review the application in the browser, then click Proceed to submit.");
                await ShowInteractionAsync(reviewReq);
                await reviewReq.Tcs.Task;
            };

            if (jm.Job.Source == "linkedin")
                success = await new LinkedInApplicator(browser).ApplyAsync(jm, selectedLetters, waitReview, progress);
            else
                success = await new IndeedApplicator(browser).ApplyAsync(jm, selectedLetters, waitReview, progress);

            jm.Applied           = success;
            jm.ApplicationStatus = success ? "submitted" : "failed";
        }

        // ── 6. Report ────────────────────────────────────────────────────────
        ReportPath = ReporterService.GenerateReport(config, allJobs.Count, matches.Count, matches);
        AddLog($"\n✔  Done! Report saved to {ReportPath}");

        await browser.CloseAsync();
        Finish();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Task ShowInteractionAsync(InteractionRequest request)
    {
        Application.Current.Dispatcher.Invoke(() => PendingInteraction = request);
        return request switch
        {
            CvChoiceRequest r     => r.Tcs.Task.ContinueWith(_ => ClearInteraction()),
            LetterChoiceRequest r => r.Tcs.Task.ContinueWith(_ => ClearInteraction()),
            ReviewRequest r       => r.Tcs.Task.ContinueWith(_ => ClearInteraction()),
            _                     => Task.CompletedTask
        };
    }

    private void ClearInteraction() =>
        Application.Current.Dispatcher.Invoke(() => PendingInteraction = null);

    private void AddLog(string msg) =>
        Application.Current.Dispatcher.Invoke(() => Log.Add(msg));

    private void Finish()
    {
        IsRunning   = false;
        CurrentStep = RunStep.Done;
        StatusText  = ReportPath is not null ? "Done — report ready" : "Done";
    }
}
