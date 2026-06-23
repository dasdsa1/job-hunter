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
    [ObservableProperty] private string      _apiKey       = "";
    [ObservableProperty] private string      _geminiModel  = "gemini-2.0-flash-lite";
    [ObservableProperty] private int         _geminiRpm    = 15;
    [ObservableProperty] private BrowserMode      _browserMode      = BrowserMode.Managed;
    [ObservableProperty] private PreferredBrowser _preferredBrowser = PreferredBrowser.Chrome;
    [ObservableProperty] private int              _cdpPort          = 9222;
    [ObservableProperty] private string      _cvPath       = "";
    [ObservableProperty] private string      _cvKey        = "cv";
    [ObservableProperty] private string      _saveStatus   = "";
    [ObservableProperty] private string      _browserStatus = "";

    public ObservableCollection<FileEntry> Letters { get; } = [];

    public SetupViewModel()
    {
        var cfg = FileConfigService.Load();
        ApiKey      = cfg.ApiKey;
        GeminiModel = cfg.GeminiModel;
        GeminiRpm   = cfg.GeminiRpm;
        BrowserMode      = cfg.BrowserMode;
        PreferredBrowser = cfg.PreferredBrowser;
        CdpPort          = cfg.CdpPort;
        CvPath      = cfg.Cv?.Path ?? "";
        CvKey       = cfg.Cv?.Key  ?? "cv";
        foreach (var l in cfg.Letters) Letters.Add(l);
    }

    [RelayCommand]
    private void LaunchBrowser()
    {
        var local  = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var progX  = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var prog   = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        var candidates = PreferredBrowser switch
        {
            PreferredBrowser.Firefox => new[]
            {
                System.IO.Path.Combine(prog,  @"Mozilla Firefox\firefox.exe"),
                System.IO.Path.Combine(progX, @"Mozilla Firefox\firefox.exe"),
            },
            PreferredBrowser.Edge => new[]
            {
                System.IO.Path.Combine(prog,  @"Microsoft\Edge\Application\msedge.exe"),
                System.IO.Path.Combine(progX, @"Microsoft\Edge\Application\msedge.exe"),
            },
            _ => new[]  // Chrome
            {
                System.IO.Path.Combine(local, @"Google\Chrome\Application\chrome.exe"),
                System.IO.Path.Combine(prog,  @"Google\Chrome\Application\chrome.exe"),
                System.IO.Path.Combine(progX, @"Google\Chrome\Application\chrome.exe"),
            }
        };

        var exe = candidates.FirstOrDefault(System.IO.File.Exists);
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
            BrowserStatus = $"{PreferredBrowser} launched on port {CdpPort} — log in to your sites, then start the job run.";
        }
        catch (Exception ex)
        {
            BrowserStatus = $"Failed to launch: {ex.Message}";
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
    private void Save()
    {
        var cfg = new AppConfig
        {
            ApiKey      = ApiKey.Trim(),
            GeminiModel = GeminiModel.Trim(),
            GeminiRpm   = GeminiRpm,
            BrowserMode      = BrowserMode,
            PreferredBrowser = PreferredBrowser,
            CdpPort          = CdpPort,
            Cv          = string.IsNullOrWhiteSpace(CvPath) ? null
                          : new FileEntry { Key = CvKey, Label = System.IO.Path.GetFileName(CvPath), Path = CvPath },
            Letters     = [.. Letters]
        };
        FileConfigService.Save(cfg);
        SaveStatus = $"Saved at {DateTime.Now:HH:mm:ss}";
    }
}
