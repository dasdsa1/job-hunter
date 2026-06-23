using Microsoft.Playwright;
using JobHunterApp.Models;

namespace JobHunterApp.Services;

public static class BrowserService
{
    public static async Task<IBrowserContext> CreateContextAsync(
        AppConfig config, IProgress<string>? log = null)
    {
        var playwright = await Playwright.CreateAsync();

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

        // ── Managed: Playwright launches and owns the browser ────────────────
        var profileDir = config.PreferredBrowser == PreferredBrowser.Firefox
            ? AppPaths.BrowserProfileFirefox
            : AppPaths.BrowserProfileChromium;

        Directory.CreateDirectory(profileDir);

        var launchOptions = new BrowserTypeLaunchPersistentContextOptions
        {
            Headless          = false,
            ViewportSize      = new ViewportSize { Width = 1280, Height = 900 },
            IgnoreHTTPSErrors = true
        };

        IBrowserContext context;

        if (config.PreferredBrowser == PreferredBrowser.Firefox)
        {
            log?.Report("🦊  Launching Firefox…");
            context = await playwright.Firefox.LaunchPersistentContextAsync(
                profileDir, launchOptions);
        }
        else
        {
            launchOptions.Args = ["--disable-blink-features=AutomationControlled", "--no-sandbox"];
            var label = config.PreferredBrowser == PreferredBrowser.Edge ? "Edge" : "Chrome";
            log?.Report($"🌐  Launching {label}…");
            context = await playwright.Chromium.LaunchPersistentContextAsync(
                profileDir, launchOptions);
        }

        log?.Report("✔  Browser ready. If you see a login page, sign in — your session is saved for next time.");
        return context;
    }
}
