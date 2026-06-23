using Microsoft.Playwright;
using JobHunterApp.Models;

namespace JobHunterApp.Services.Applicators;

public class IndeedApplicator(IBrowserContext context)
{
    public async Task<bool> ApplyAsync(
        JobMatch jobMatch,
        IEnumerable<FileEntry> letters,
        Func<Task> waitForUserReview,
        IProgress<string> log)
    {
        var job = jobMatch.Job;

        if (!job.IsEasyApply)
        {
            log.Report($"No Indeed Apply button — opening {job.Url} for manual application.");
            var manualPage = await context.NewPageAsync();
            await manualPage.GotoAsync(job.Url);
            await waitForUserReview();
            await manualPage.CloseAsync();
            return true;
        }

        var page = await context.NewPageAsync();
        try
        {
            await page.GotoAsync(job.Url, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });

            var applyBtn = await page.WaitForSelectorAsync(
                "[data-testid='indeedApplyButton'], .indeed-apply-button, button:has-text('Apply now')",
                new() { Timeout = 10_000 });
            await applyBtn!.ClickAsync();
            await page.WaitForTimeoutAsync(2_000);

            await FillFormStepsAsync(page, jobMatch.CoverLetter ?? "");
            await LetterUploader.TryUploadAsync(page, letters);

            log.Report("Form pre-filled — waiting for your review.");
            await waitForUserReview();

            var submitted = await ClickSubmitAsync(page);
            log.Report(submitted ? "Application submitted!" : "Submit button not found — submitted manually.");
            return true;
        }
        catch (Exception ex)
        {
            log.Report($"Indeed Apply failed: {ex.Message}");
            await page.GotoAsync(job.Url);
            await waitForUserReview();
            return false;
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private static async Task FillFormStepsAsync(IPage page, string coverLetter)
    {
        for (var step = 0; step < 8; step++)
        {
            if (!string.IsNullOrWhiteSpace(coverLetter))
            {
                var textareas = await page.QuerySelectorAllAsync("textarea");
                foreach (var ta in textareas)
                {
                    var name        = await ta.GetAttributeAsync("name") ?? "";
                    var placeholder = await ta.GetAttributeAsync("placeholder") ?? "";
                    var combined    = $"{name} {placeholder}".ToLowerInvariant();
                    if (combined.Contains("cover") || combined.Contains("letter") || combined.Contains("message"))
                    {
                        var existing = await ta.InputValueAsync();
                        if (string.IsNullOrWhiteSpace(existing)) await ta.FillAsync(coverLetter);
                    }
                }
            }

            var selects = await page.QuerySelectorAllAsync("select");
            foreach (var sel in selects)
            {
                var val = await sel.InputValueAsync();
                if (string.IsNullOrEmpty(val))
                {
                    var opts = await sel.QuerySelectorAllAsync("option");
                    if (opts.Count > 1) await sel.SelectOptionAsync(new SelectOptionValue { Index = 1 });
                }
            }

            var next = await page.QuerySelectorAsync(
                "button[data-testid='ia-continueButton'], button:has-text('Continue'), button:has-text('Next')");
            if (next is null) break;

            var text = await next.TextContentAsync();
            if ((text ?? "").Contains("Submit", StringComparison.OrdinalIgnoreCase)) break;

            await next.ClickAsync();
            await page.WaitForTimeoutAsync(1_200);
        }
    }

    private static async Task<bool> ClickSubmitAsync(IPage page)
    {
        var btn = await page.QuerySelectorAsync(
            "button[data-testid='ia-continueButton']:has-text('Submit'), button:has-text('Submit my application'), button:has-text('Submit application')");
        if (btn is null) return false;
        await btn.ClickAsync();
        await page.WaitForTimeoutAsync(2_000);
        return true;
    }
}
