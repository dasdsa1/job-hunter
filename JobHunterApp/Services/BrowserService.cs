using Microsoft.Playwright;
using JobHunterApp.Models;

namespace JobHunterApp.Services;

public static class BrowserService
{
    public static async Task<IBrowserContext> CreateContextAsync(
        AppConfig config, IProgress<string>? log = null)
    {
        var playwright = await Playwright.CreateAsync();

        // ── CDP mode: connect to a browser the user launched manually ────────
        if (config.BrowserMode == BrowserMode.ConnectToBrowser)
        {
            log?.Report($"🔌  Connecting to browser on port {config.CdpPort}…");
            var browser = await playwright.Chromium.ConnectOverCDPAsync(
                $"http://localhost:{config.CdpPort}");
            log?.Report("✔  Connected to existing browser session.");
            return browser.Contexts.Count > 0
                ? browser.Contexts[0]
                : await browser.NewContextAsync();
        }

        // ── Managed mode: app opens the browser, user logs in once ───────────
        // Profile is saved per-browser so sessions persist between runs.
        log?.Report("🌐  Opening browser — log in to LinkedIn / Indeed if prompted…");
        log?.Report("    Your session will be saved automatically for next time.");

        if (config.PreferredBrowser == PreferredBrowser.Firefox)
        {
            Directory.CreateDirectory(AppPaths.BrowserProfileFirefox);
            var ctx = await playwright.Firefox.LaunchPersistentContextAsync(
                AppPaths.BrowserProfileFirefox,
                new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless          = false,
                    ViewportSize      = new ViewportSize { Width = 1280, Height = 900 },
                    IgnoreHTTPSErrors = true
                });
            log?.Report("✔  Firefox ready.");
            return ctx;
        }

        // Chrome or Edge — use system-installed browser via Channel so it opens
        // the real browser the user knows, not Playwright's bundled Chromium.
        Directory.CreateDirectory(AppPaths.BrowserProfileChromium);
        var channel = config.PreferredBrowser == PreferredBrowser.Edge ? "msedge" : "chrome";
        var label   = config.PreferredBrowser == PreferredBrowser.Edge ? "Edge"   : "Chrome";

        var context = await playwright.Chromium.LaunchPersistentContextAsync(
            AppPaths.BrowserProfileChromium,
            new BrowserTypeLaunchPersistentContextOptions
            {
                Headless          = false,
                Channel           = channel,   // ← uses system Chrome/Edge, not bundled Chromium
                ViewportSize      = new ViewportSize { Width = 1280, Height = 900 },
                IgnoreHTTPSErrors = true,
                Args              = ["--disable-blink-features=AutomationControlled"]
            });

        log?.Report($"✔  {label} ready.");
        return context;
    }
}
