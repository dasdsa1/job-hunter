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
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });

            // Wait (forever) until EITHER the jobs results appear OR any login/authwall indicator.
            // This avoids the old 15-second hard timeout that closed the browser before login.
            await page.WaitForFunctionAsync(@"() => {
                const hasResults = document.querySelector(
                    '.jobs-search-results-list, .jobs-search__results-list, .scaffold-layout__list');
                const needsLogin =
                    location.href.includes('/login') ||
                    location.href.includes('/authwall') ||
                    !!document.querySelector('#username, .login__form, [data-id=""auth-login""]');
                return !!(hasResults || needsLogin);
            }", arg: null, new PageWaitForFunctionOptions { Timeout = 0 });

            var needsLogin = await page.EvaluateAsync<bool>(@"() =>
                location.href.includes('/login')  ||
                location.href.includes('/authwall') ||
                !!document.querySelector('#username, .login__form, [data-id=""auth-login""]')");

            if (needsLogin)
            {
                log.Report("🔐  Please log in to LinkedIn in the browser window.");
                log.Report("    The job hunt will resume automatically once you are logged in.");
                // Timeout = 0 — wait indefinitely, never close the browser
                await page.WaitForFunctionAsync(@"() =>
                    !location.href.includes('/login') &&
                    !location.href.includes('/authwall') &&
                    !document.querySelector('#username, .login__form')",
                    arg: null, new PageWaitForFunctionOptions { Timeout = 0 });
                log.Report("✔  Logged in to LinkedIn — navigating to job results…");
                await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            }

            // Now wait for the actual job results (give it 30 s; we're definitely logged in at this point)
            await page.WaitForSelectorAsync(
                ".jobs-search-results-list, .jobs-search__results-list, .scaffold-layout__list",
                new() { Timeout = 30_000 });

            for (var i = 0; i < 3; i++)
            {
                await page.EvaluateAsync("() => window.scrollBy(0, 600)");
                await page.WaitForTimeoutAsync(700);
            }

            var cards = await page.QuerySelectorAllAsync(
                "li.jobs-search-results__list-item, li[data-occludable-job-id]");

            for (var i = 0; i < Math.Min(cards.Count, config.MaxJobsPerSite); i++)
            {
                try
                {
                    await cards[i].ClickAsync();
                    await page.WaitForTimeoutAsync(1_200);
                    var job = await ExtractJobAsync(page, i.ToString());
                    if (job is null) continue;
                    if (config.LinkedInEasyApplyOnly && !IsEasyApply(job)) continue;
                    jobs.Add(job);
                    log.Report($"LinkedIn: scraped {jobs.Count} job(s)…");
                }
                catch { }
            }

            log.Report($"LinkedIn: found {jobs.Count} job(s)");
        }
        catch (Exception ex)
        {
            log.Report($"LinkedIn scraping failed: {ex.Message}");
        }
        finally
        {
            await page.CloseAsync();
        }

        return jobs;
    }

    /// Returns true when the job has the LinkedIn Easy Apply button (one-click in-platform apply).
    private static bool IsEasyApply(JobListing job) => job.IsEasyApply;

    private static string BuildUrl(SearchConfig c)
    {
        var kw     = string.Join(" ", new[] { c.JobTitle, c.Keywords }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var query  = $"keywords={Uri.EscapeDataString(kw)}&location={Uri.EscapeDataString(c.Location)}";
        if (c.LinkedInEasyApplyOnly) query += "&f_LF=f_AL";
        return $"https://www.linkedin.com/jobs/search/?{query}";
    }

    private static async Task<JobListing?> ExtractJobAsync(IPage page, string idx)
    {
        var title   = await SafeTextAsync(page, [".jobs-unified-top-card__job-title", ".job-details-jobs-unified-top-card__job-title", "h1.t-24"]);
        var company = await SafeTextAsync(page, [".jobs-unified-top-card__company-name a", ".jobs-unified-top-card__company-name", ".job-details-jobs-unified-top-card__company-name"]);
        var loc     = await SafeTextAsync(page, [".jobs-unified-top-card__bullet", ".job-details-jobs-unified-top-card__bullet"]);
        var desc    = await SafeTextAsync(page, [".jobs-description-content__text", ".jobs-description__content", "#job-details"]);

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(company)) return null;

        var btn         = await page.QuerySelectorAsync("button.jobs-apply-button");
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
