using Microsoft.Playwright;
using JobHunterApp.Models;

namespace JobHunterApp.Services.Applicators;

public static class LetterUploader
{
    private static readonly string[] Keywords = ["recommend", "letter", "additional", "supporting", "reference"];

    public static async Task TryUploadAsync(IPage page, IEnumerable<FileEntry> letters)
    {
        var paths = letters.Select(l => l.Path).Where(File.Exists).ToArray();
        if (paths.Length == 0) return;

        var inputs = await page.QuerySelectorAllAsync("input[type='file']");
        foreach (var input in inputs)
        {
            var label = await input.EvaluateAsync<string>("""
                el => {
                    const id  = el.getAttribute('id');
                    const lbl = id ? document.querySelector(`label[for="${id}"]`) : null;
                    return (lbl?.textContent ?? el.getAttribute('aria-label') ?? el.getAttribute('name') ?? '').toLowerCase();
                }
                """);

            if (!Keywords.Any(kw => label.Contains(kw))) continue;

            try { await input.SetInputFilesAsync(paths); }
            catch { }
            return;
        }
    }
}
