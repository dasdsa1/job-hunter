using Microsoft.Playwright;
using JobHunterApp.Models;

namespace JobHunterApp.Services.Applicators;

public class LinkedInApplicator(IBrowserContext context)
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
            log.Report($"Not Easy Apply — opening {job.Url} for manual application.");
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
                "button.jobs-apply-button:has-text('Easy Apply'), button[aria-label*='Easy Apply']",
                new() { Timeout = 10_000 });
            await applyBtn!.ClickAsync();
            await page.WaitForTimeoutAsync(1_500);

            await FillModalStepsAsync(page, jobMatch.CoverLetter ?? "");
            await LetterUploader.TryUploadAsync(page, letters);

            log.Report("Form pre-filled — waiting for your review.");
            await waitForUserReview();

            var submitted = await ClickSubmitAsync(page);
            log.Report(submitted ? "Application submitted!" : "Submit button not found — submitted manually.");
            return true;
        }
        catch (Exception ex)
        {
            log.Report($"Easy Apply failed: {ex.Message}");
            await page.GotoAsync(job.Url);
            await waitForUserReview();
            return false;
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private static async Task FillModalStepsAsync(IPage page, string coverLetter)
    {
        for (var step = 0; step < 10; step++)
        {
            await FillCurrentStepAsync(page, coverLetter);

            var next = await page.QuerySelectorAsync(
                "button[aria-label='Continue to next step'], button:has-text('Next'), footer button:last-child");
            if (next is null) break;

            var text = await next.TextContentAsync();
            if ((text ?? "").Contains("Submit", StringComparison.OrdinalIgnoreCase)) break;

            await next.ClickAsync();
            await page.WaitForTimeoutAsync(1_000);
        }
    }

    private static async Task FillCurrentStepAsync(IPage page, string coverLetter)
    {
        if (!string.IsNullOrWhiteSpace(coverLetter))
        {
            var textareas = await page.QuerySelectorAllAsync("textarea");
            foreach (var ta in textareas)
            {
                var label = await ta.EvaluateAsync<string>("""
                    el => {
                        const id  = el.getAttribute('id');
                        const lbl = id ? document.querySelector(`label[for="${id}"]`) : null;
                        return (lbl?.textContent ?? el.getAttribute('aria-label') ?? el.getAttribute('placeholder') ?? '').toLowerCase();
                    }
                    """);

                if (label.Contains("cover") || label.Contains("letter") || label.Contains("additional"))
                {
                    var existing = await ta.InputValueAsync();
                    if (string.IsNullOrWhiteSpace(existing))
                        await ta.FillAsync(coverLetter);
                }
            }
        }

        var groups = await page.QuerySelectorAllAsync("fieldset");
        foreach (var g in groups)
        {
            var checked_ = await g.QuerySelectorAsync("input[type='radio']:checked");
            if (checked_ is null)
            {
                var first = await g.QuerySelectorAsync("input[type='radio']");
                if (first is not null) await first.CheckAsync();
            }
        }
    }

    private static async Task<bool> ClickSubmitAsync(IPage page)
    {
        var btn = await page.QuerySelectorAsync(
            "button[aria-label='Submit application'], button:has-text('Submit application')");
        if (btn is null) return false;
        await btn.ClickAsync();
        await page.WaitForTimeoutAsync(2_000);
        return true;
    }
}
