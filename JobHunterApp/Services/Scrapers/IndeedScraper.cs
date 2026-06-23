using Microsoft.Playwright;
using JobHunterApp.Models;

namespace JobHunterApp.Services.Scrapers;

public static class IndeedScraper
{
    public static async Task<List<JobListing>> ScrapeAsync(
        IBrowserContext context, SearchConfig config, IProgress<string> log)
    {
        var page = await context.NewPageAsync();
        var jobs = new List<JobListing>();

        try
        {
            await page.AddInitScriptAsync("Object.defineProperty(navigator,'webdriver',{get:()=>false})");
            var url = BuildUrl(config);
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });

            // Poll using CSS selectors and URL checks only — no eval() to avoid CSP issues.
            bool loginDetected = false;
            while (true)
            {
                try
                {
                    if (await page.QuerySelectorAsync("[data-jk], .jobsearch-ResultsList li, .job_seen_beacon")
                        is not null) break;

                    bool onLogin = page.Url.Contains("/account/login") || page.Url.Contains("/promo/resume")
                        || await page.QuerySelectorAsync(
                            "#login-email, #ifl-InputFormField-3, [data-testid='auth-page-email-input']")
                            is not null;

                    if (onLogin && !loginDetected)
                    {
                        log.Report("🔐  Please log in to Indeed in the browser window.");
                        log.Report("    The job hunt will resume automatically once you are logged in.");
                        loginDetected = true;
                    }
                    else if (loginDetected && !onLogin)
                    {
                        log.Report("✔  Logged in to Indeed — navigating to job results…");
                        await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
                        loginDetected = false;
                    }
                }
                catch { /* page is mid-navigation — retry next tick */ }

                await Task.Delay(1_000);
            }

            var cards = await page.QuerySelectorAllAsync(
                "[data-jk], .jobsearch-ResultsList li.css-1ac2h1w, .result");

            for (var i = 0; i < Math.Min(cards.Count, config.MaxJobsPerSite); i++)
            {
                try
                {
                    await cards[i].ScrollIntoViewIfNeededAsync();
                    await cards[i].ClickAsync();
                    await page.WaitForTimeoutAsync(1_200);
                    var job = await ExtractJobAsync(page, i);
                    if (job is null) continue;
                    if (config.IndeedApplyOnly && !IsIndeedApply(job)) continue;
                    jobs.Add(job);
                    log.Report($"Indeed: scraped {jobs.Count} job(s)…");
                }
                catch { }
            }

            log.Report($"Indeed: found {jobs.Count} job(s)");
        }
        catch (Exception ex)
        {
            log.Report($"Indeed scraping failed: {ex.Message}");
        }
        finally
        {
            await page.CloseAsync();
        }

        return jobs;
    }

    /// Returns true when the job shows the Indeed Apply button (apply without leaving Indeed).
    private static bool IsIndeedApply(JobListing job) => job.IsEasyApply;

    private static string BuildUrl(SearchConfig c)
    {
        var q = string.Join(" ", new[] { c.JobTitle, c.Keywords }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return $"https://www.indeed.com/jobs?q={Uri.EscapeDataString(q)}&l={Uri.EscapeDataString(c.Location)}";
    }

    private static async Task<JobListing?> ExtractJobAsync(IPage page, int idx)
    {
        var title   = await SafeTextAsync(page, [".jobsearch-JobInfoHeader-title", "h1.jobsearch-JobInfoHeader-title", "[data-testid=\"simpleTitle\"]"]);
        var company = await SafeTextAsync(page, ["[data-testid=\"inlineHeader-companyName\"] a", "[data-testid=\"inlineHeader-companyName\"]", "span.companyName"]);
        var loc     = await SafeTextAsync(page, ["[data-testid=\"job-location\"]", "div.companyLocation"]);
        var desc    = await SafeTextAsync(page, ["#jobDescriptionText", ".jobsearch-jobDescriptionText"]);

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(company)) return null;

        var applyBtn    = await page.QuerySelectorAsync("[data-testid=\"indeedApplyButton\"], .indeed-apply-button");
        var isEasyApply = applyBtn is not null;

        var url   = page.Url;
        var match = System.Text.RegularExpressions.Regex.Match(url, @"[?&]jk=([a-zA-Z0-9]+)");
        var id    = match.Success ? $"indeed-{match.Groups[1].Value}" : $"indeed-{idx}";

        return new JobListing
        {
            Id          = id,
            Title       = title.Trim(),
            Company     = company.Trim(),
            Location    = loc.Trim(),
            Description = desc[..Math.Min(desc.Length, 3_500)].Trim(),
            Url         = url,
            Source      = "indeed",
            IsEasyApply = isEasyApply
        };
    }

    private static async Task<string> SafeTextAsync(IPage page, string[] selectors)
    {
        foreach (var sel in selectors)
        {
            try
            {
                var el = await page.QuerySelectorAsync(sel);
                if (el is null) continue;
                var text = await el.TextContentAsync();
                if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
            }
            catch { }
        }
        return "";
    }
}
