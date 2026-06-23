using Microsoft.Playwright;
using JobHunterApp.Models;

namespace JobHunterApp.Services;

public static class BrowserService
{
    public static async Task<IBrowserContext> CreateContextAsync(AppConfig config)
    {
        var playwright = await Playwright.CreateAsync();

        if (config.BrowserMode == BrowserMode.ConnectToBrowser)
        {
            var browser = await playwright.Chromium.ConnectOverCDPAsync(
                $"http://localhost:{config.CdpPort}");
            return browser.Contexts.Count > 0
                ? browser.Contexts[0]
                : await browser.NewContextAsync();
        }

        // Managed: Playwright owns its own persistent Chromium profile
        Directory.CreateDirectory(AppPaths.BrowserProfile);
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
