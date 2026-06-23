using Microsoft.Playwright;
using JobHunterApp.Models;

namespace JobHunterApp.Services;

public static class BrowserService
{
    public static async Task<IBrowserContext> CreateContextAsync()
    {
        Directory.CreateDirectory(AppPaths.BrowserProfile);
        var playwright = await Playwright.CreateAsync();
        return await playwright.Chromium.LaunchPersistentContextAsync(
            AppPaths.BrowserProfile,
            new BrowserTypeLaunchPersistentContextOptions
            {
                Headless     = false,
                ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
                Args         = ["--disable-blink-features=AutomationControlled", "--no-sandbox"],
                IgnoreHTTPSErrors = true
            });
    }
}
