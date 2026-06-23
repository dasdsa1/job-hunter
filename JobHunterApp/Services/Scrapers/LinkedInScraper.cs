using Microsoft.Playwright;
using JobHunterApp.Models;

namespace JobHunterApp.Services.Scrapers;

public static class LinkedInScraper
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
            log.Report($"LinkedIn: navigating to {url}");
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });
            log.Report($"LinkedIn: landed at {Shorten(page.Url)}");

            // Poll using CSS selectors and URL checks only — LinkedIn's CSP blocks eval(),
            // so WaitForFunctionAsync / EvaluateAsync must not be used here.
            bool loginDetected = false;
            while (true)
            {
                try
                {
                    if (await page.QuerySelectorAsync(
                        ".jobs-search-results-list, .jobs-search__results-list, .scaffold-layout__list")
                        is not null) break;

                    bool onLogin = page.Url.Contains("/login") || page.Url.Contains("/authwall")
                        || await page.QuerySelectorAsync("#username, .login__form") is not null;

                    if (onLogin && !loginDetected)
                    {
                        log.Report("🔐  Please log in to LinkedIn in the browser window.");
                        log.Report("    The job hunt will resume automatically once you are logged in.");
                        loginDetected = true;
                    }
                    else if (loginDetected && !onLogin)
                    {
                        log.Report($"✔  Logged in — at {Shorten(page.Url)}");
                        log.Report("    Navigating to job results…");
                        await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
                        log.Report($"LinkedIn: results page at {Shorten(page.Url)}");
                        loginDetected = false;
                    }
                }
                catch { /* page is mid-navigation — retry next tick */ }

                await Task.Delay(1_000);
            }

            log.Report($"LinkedIn: results container found at {Shorten(page.Url)}");

            // Scroll the results panel — move mouse into it first so the wheel hits
            // the correct scrollable element (the left panel, not the window).
            var panel = await page.QuerySelectorAsync(
                ".jobs-search-results-list, .jobs-search__results-list, .scaffold-layout__list");
            if (panel is not null)
            {
                var box = await panel.BoundingBoxAsync();
                if (box is not null)
                    await page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
            }
            for (var i = 0; i < 5; i++)
            {
                await page.Mouse.WheelAsync(0, 600);
                await Task.Delay(600);
            }
            // Scroll back to top so cards are in view
            await page.Mouse.WheelAsync(0, -9999);
            await Task.Delay(500);

            // ── Selector probe ───────────────────────────────────────────────────
            // LinkedIn renames CSS classes often; try many patterns and log counts
            // so we can see immediately which selector LinkedIn currently uses.
            var cA = await page.QuerySelectorAllAsync("li.jobs-search-results__list-item");
            var cB = await page.QuerySelectorAllAsync("li[data-occludable-job-id]");
            var cC = await page.QuerySelectorAllAsync(".scaffold-layout__list-item");
            var cD = await page.QuerySelectorAllAsync(".job-card-container--clickable");
            var cE = await page.QuerySelectorAllAsync("[data-job-id]");
            log.Report($"LinkedIn: card probe A — " +
                       $"list-item:{cA.Count}  occludable:{cB.Count}  " +
                       $"scaffold:{cC.Count}  clickable:{cD.Count}  job-id:{cE.Count}");

            // Deep probe using CSS attribute-contains selectors (CSP-safe, pure DOM)
            var dA = await page.QuerySelectorAllAsync("[class*='job-card']");
            var dB = await page.QuerySelectorAllAsync("[class*='JobCard']");
            var dC = await page.QuerySelectorAllAsync("[class*='result-card']");
            var dD = await page.QuerySelectorAllAsync("[data-entity-urn*='jobPosting']");
            var dE = await page.QuerySelectorAllAsync("[data-view-name*='job-card']");
            var dF = await page.QuerySelectorAllAsync("article");
            var dG = await page.QuerySelectorAllAsync("li[class]");
            log.Report($"LinkedIn: card probe B — " +
                       $"*job-card*:{dA.Count}  *JobCard*:{dB.Count}  *result-card*:{dC.Count}  " +
                       $"entity-urn:{dD.Count}  view-name:{dE.Count}  article:{dF.Count}  li[class]:{dG.Count}");

            // Use whichever selector found the most cards
            var cards = new[] { cA, cB, cC, cD, cE, dA, dB, dC, dD, dE, dF, dG }
                .OrderByDescending(c => c.Count)
                .First();
            var limit = Math.Min(cards.Count, config.MaxJobsPerSite);
            log.Report($"LinkedIn: processing {limit} of {cards.Count} card(s)");

            for (var i = 0; i < limit; i++)
            {
                try
                {
                    log.Report($"LinkedIn: card {i + 1}/{limit}…");
                    await cards[i].ClickAsync();
                    await Task.Delay(1_500);
                    var job = await ExtractJobAsync(page, i.ToString());
                    if (job is null)
                    {
                        log.Report($"LinkedIn:   card {i + 1} — no title/company found (selectors may have drifted)");
                        continue;
                    }
                    if (config.LinkedInEasyApplyOnly && !IsEasyApply(job))
                    {
                        log.Report($"LinkedIn:   '{job.Title}' skipped — not Easy Apply");
                        continue;
                    }
                    jobs.Add(job);
                    log.Report($"LinkedIn:   ✔  {job.Title} @ {job.Company}");
                }
                catch (Exception ex) { log.Report($"LinkedIn:   card {i + 1} error: {ex.Message}"); }
            }

            log.Report($"LinkedIn: done — {jobs.Count} job(s) collected");
        }
        catch (Exception ex)
        {
            log.Report($"LinkedIn scraping failed: {ex.Message}");
            AppLogger.Exception("LinkedInScraper", ex);
        }
        finally
        {
            await page.CloseAsync();
        }

        return jobs;
    }

    private static bool IsEasyApply(JobListing job) => job.IsEasyApply;

    private static string Shorten(string url) =>
        url.Length > 90 ? url[..90] + "…" : url;

    private static string BuildUrl(SearchConfig c)
    {
        var kw    = string.Join(" ", new[] { c.JobTitle, c.Keywords }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var query = $"keywords={Uri.EscapeDataString(kw)}&location={Uri.EscapeDataString(c.Location)}";
        if (c.LinkedInEasyApplyOnly) query += "&f_LF=f_AL";
        return $"https://www.linkedin.com/jobs/search/?{query}";
    }

    private static async Task<JobListing?> ExtractJobAsync(IPage page, string idx)
    {
        var title   = await SafeTextAsync(page, [
            ".jobs-unified-top-card__job-title",
            ".job-details-jobs-unified-top-card__job-title",
            "h1.t-24", "h1"
        ]);
        var company = await SafeTextAsync(page, [
            ".jobs-unified-top-card__company-name a",
            ".jobs-unified-top-card__company-name",
            ".job-details-jobs-unified-top-card__company-name",
            ".topcard__org-name-link", ".topcard__flavor"
        ]);
        var loc = await SafeTextAsync(page, [
            ".jobs-unified-top-card__bullet",
            ".job-details-jobs-unified-top-card__bullet",
            ".topcard__flavor--bullet"
        ]);
        var desc = await SafeTextAsync(page, [
            ".jobs-description-content__text",
            ".jobs-description__content",
            "#job-details",
            ".description__text"
        ]);

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(company)) return null;

        var btn         = await page.QuerySelectorAsync("button.jobs-apply-button, .jobs-apply-button");
        var btnText     = btn is not null ? await btn.TextContentAsync() : "";
        var isEasyApply = (btnText ?? "").Contains("Easy Apply", StringComparison.OrdinalIgnoreCase);

        var url   = page.Url;
        var match = System.Text.RegularExpressions.Regex.Match(url, @"/jobs/view/(\d+)");
        var id    = match.Success ? $"linkedin-{match.Groups[1].Value}" : $"linkedin-{idx}";

        return new JobListing
        {
            Id          = id,
            Title       = title.Trim(),
            Company     = company.Trim(),
            Location    = loc.Trim(),
            Description = desc[..Math.Min(desc.Length, 3_500)].Trim(),
            Url         = url,
            Source      = "linkedin",
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
