using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JobHunterApp.Models;
using JobHunterApp.Services;
using Microsoft.Win32;

namespace JobHunterApp.ViewModels;

public partial class SetupViewModel : ObservableObject
{
    [ObservableProperty] private LlmProvider _provider     = LlmProvider.Gemini;
    [ObservableProperty] private string      _apiKey       = "";
    [ObservableProperty] private string      _geminiModel  = "gemini-flash-lite-latest";
    [ObservableProperty] private int         _geminiRpm    = 15;
    [ObservableProperty] private BrowserMode      _browserMode      = BrowserMode.Managed;
    [ObservableProperty] private PreferredBrowser _preferredBrowser = PreferredBrowser.Chrome;
    [ObservableProperty] private int              _cdpPort          = 9222;
    [ObservableProperty] private string      _adzunaAppId   = "";
    [ObservableProperty] private string      _adzunaAppKey  = "";
    [ObservableProperty] private string      _adzunaCountry = "us";
    [ObservableProperty] private string      _cvPath       = "";
    [ObservableProperty] private string      _cvKey        = "cv";
    [ObservableProperty] private string      _saveStatus   = "";
    [ObservableProperty] private string      _browserStatus = "";
    [ObservableProperty] private string      _geminiTestStatus = "";
    [ObservableProperty] private bool        _geminiTestOk;
    [ObservableProperty] private bool        _geminiTesting;
    [ObservableProperty] private string      _adzunaTestStatus = "";
    [ObservableProperty] private bool        _adzunaTestOk;
    [ObservableProperty] private bool        _adzunaTesting;

    public ObservableCollection<FileEntry> Letters { get; } = [];

    partial void OnProviderChanged(LlmProvider value)
    {
        GeminiModel = Services.LlmServiceFactory.DefaultModel(value);
        GeminiRpm   = value == LlmProvider.Gemini ? 15 : 30;
    }

    public SetupViewModel()
    {
        var cfg = FileConfigService.Load();
        Provider    = cfg.Provider;
        ApiKey      = cfg.ApiKey;
        GeminiModel = cfg.GeminiModel;
        GeminiRpm   = cfg.GeminiRpm;
        BrowserMode      = cfg.BrowserMode;
        PreferredBrowser = cfg.PreferredBrowser;
        CdpPort          = cfg.CdpPort;
        AdzunaAppId   = cfg.AdzunaAppId;
        AdzunaAppKey  = cfg.AdzunaAppKey;
        AdzunaCountry = string.IsNullOrWhiteSpace(cfg.AdzunaCountry) ? "us" : cfg.AdzunaCountry;
        CvPath      = cfg.Cv?.Path ?? "";
        CvKey       = cfg.Cv?.Key  ?? "cv";
        foreach (var l in cfg.Letters) Letters.Add(l);
    }

    // Opens the browser with the app's persistent profile so the user can log in,
    // configure LinkedIn settings, etc. — all saved automatically for job hunt runs.
    [RelayCommand]
    private void OpenBrowser()
    {
        var (exe, args) = BrowserExeAndArgs(openUrl: "https://www.linkedin.com/jobs/");
        if (exe is null)
        {
            BrowserStatus = $"{PreferredBrowser} executable not found.";
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo { FileName = exe, Arguments = args, UseShellExecute = false });
            BrowserStatus = "Browser opened — log in and configure as needed. Your session is saved automatically.";
        }
        catch (Exception ex) { BrowserStatus = $"Failed to open: {ex.Message}"; }
    }

    // Launches the browser with the CDP debug port for advanced / manual-connect mode.
    [RelayCommand]
    private void LaunchBrowser()
    {
        var (exe, _) = BrowserExeAndArgs(openUrl: null);
        if (exe is null)
        {
            BrowserStatus = $"{PreferredBrowser} not found. Launch it manually with --remote-debugging-port={CdpPort}";
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName        = exe,
                Arguments       = $"--remote-debugging-port={CdpPort}",
                UseShellExecute = false
            });
            BrowserStatus = $"{PreferredBrowser} launched on port {CdpPort} — log in, then start the job run.";
        }
        catch (Exception ex) { BrowserStatus = $"Failed to launch: {ex.Message}"; }
    }

    private (string? exe, string args) BrowserExeAndArgs(string? openUrl)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var progX = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var prog  = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        if (PreferredBrowser == PreferredBrowser.Firefox)
        {
            var exe = new[]
            {
                System.IO.Path.Combine(prog,  @"Mozilla Firefox\firefox.exe"),
                System.IO.Path.Combine(progX, @"Mozilla Firefox\firefox.exe"),
            }.FirstOrDefault(System.IO.File.Exists);
            var profileDir = Models.AppPaths.BrowserProfileFirefox;
            System.IO.Directory.CreateDirectory(profileDir);
            var args = $"-profile \"{profileDir}\"" + (openUrl is not null ? $" \"{openUrl}\"" : "");
            return (exe, args);
        }
        else
        {
            var candidates = PreferredBrowser == PreferredBrowser.Edge
                ? new[]
                {
                    System.IO.Path.Combine(prog,  @"Microsoft\Edge\Application\msedge.exe"),
                    System.IO.Path.Combine(progX, @"Microsoft\Edge\Application\msedge.exe"),
                }
                : new[]
                {
                    System.IO.Path.Combine(local, @"Google\Chrome\Application\chrome.exe"),
                    System.IO.Path.Combine(prog,  @"Google\Chrome\Application\chrome.exe"),
                    System.IO.Path.Combine(progX, @"Google\Chrome\Application\chrome.exe"),
                };
            var exe = candidates.FirstOrDefault(System.IO.File.Exists);
            var profileDir = Models.AppPaths.BrowserProfileChromium;
            System.IO.Directory.CreateDirectory(profileDir);
            var url  = openUrl is not null ? $" \"{openUrl}\"" : "";
            var args = $"--user-data-dir=\"{profileDir}\"{url}";
            return (exe, args);
        }
    }

    [RelayCommand]
    private void BrowseCv()
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select your CV",
            Filter = "Documents (*.pdf;*.docx)|*.pdf;*.docx|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
            CvPath = dlg.FileName;
    }

    [RelayCommand]
    private void BrowseLetter(FileEntry letter)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select recommendation letter",
            Filter = "Documents (*.pdf;*.docx)|*.pdf;*.docx|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
            letter.Path = dlg.FileName;
    }

    [RelayCommand]
    private void AddLetter()
    {
        Letters.Add(new FileEntry
        {
            Key   = $"letter-{Letters.Count + 1}",
            Label = "",
            Path  = ""
        });
    }

    [RelayCommand]
    private void RemoveLetter(FileEntry letter) => Letters.Remove(letter);

    [RelayCommand]
    private async Task TestGemini()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            GeminiTestOk = false;
            GeminiTestStatus = "Enter an API key first.";
            return;
        }
        GeminiTesting = true;
        GeminiTestStatus = "Testing…";
        try
        {
            var rateLimiter = new RateLimiter(GeminiRpm);
            var testCfg = new AppConfig { Provider = Provider, ApiKey = ApiKey.Trim(), GeminiModel = GeminiModel.Trim() };
            var llm = LlmServiceFactory.Create(testCfg, rateLimiter);
            var (ok, message) = await llm.TestConnectionAsync();
            GeminiTestOk = ok;
            GeminiTestStatus = message;
        }
        finally { GeminiTesting = false; }
    }

    [RelayCommand]
    private async Task TestAdzuna()
    {
        if (string.IsNullOrWhiteSpace(AdzunaAppId) || string.IsNullOrWhiteSpace(AdzunaAppKey))
        {
            AdzunaTestOk = false;
            AdzunaTestStatus = "Enter App ID and App Key first.";
            return;
        }
        AdzunaTesting = true;
        AdzunaTestStatus = "Testing…";
        try
        {
            var cfg = new AppConfig
            {
                AdzunaAppId   = AdzunaAppId.Trim(),
                AdzunaAppKey  = AdzunaAppKey.Trim(),
                AdzunaCountry = AdzunaCountry.Trim()
            };
            var (ok, message) = await Services.Sources.AdzunaSource.TestConnectionAsync(cfg);
            AdzunaTestOk = ok;
            AdzunaTestStatus = message;
        }
        finally { AdzunaTesting = false; }
    }

    [RelayCommand]
    private void Save()
    {
        var cfg = new AppConfig
        {
            Provider    = Provider,
            ApiKey      = ApiKey.Trim(),
            GeminiModel = GeminiModel.Trim(),
            GeminiRpm   = GeminiRpm,
            BrowserMode      = BrowserMode,
            PreferredBrowser = PreferredBrowser,
            CdpPort          = CdpPort,
            AdzunaAppId   = AdzunaAppId.Trim(),
            AdzunaAppKey  = AdzunaAppKey.Trim(),
            AdzunaCountry = AdzunaCountry.Trim(),
            Cv          = string.IsNullOrWhiteSpace(CvPath) ? null
                          : new FileEntry { Key = CvKey, Label = System.IO.Path.GetFileName(CvPath), Path = CvPath },
            Letters     = [.. Letters]
        };
        FileConfigService.Save(cfg);
        SaveStatus = $"Saved at {DateTime.Now:HH:mm:ss}";
    }
}
