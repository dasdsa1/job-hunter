using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JobHunterApp.Models;
using JobHunterApp.Services;
using Microsoft.Win32;

namespace JobHunterApp.ViewModels;

public partial class SetupViewModel : ObservableObject
{
    [ObservableProperty] private string _apiKey      = "";
    [ObservableProperty] private string _geminiModel = "gemini-2.0-flash-lite";
    [ObservableProperty] private int    _geminiRpm   = 15;
    [ObservableProperty] private string _cvPath      = "";
    [ObservableProperty] private string _cvKey       = "cv";
    [ObservableProperty] private string _saveStatus  = "";

    public ObservableCollection<FileEntry> Letters { get; } = [];

    public SetupViewModel()
    {
        var cfg = FileConfigService.Load();
        ApiKey      = cfg.ApiKey;
        GeminiModel = cfg.GeminiModel;
        GeminiRpm   = cfg.GeminiRpm;
        CvPath      = cfg.Cv?.Path  ?? "";
        CvKey       = cfg.Cv?.Key   ?? "cv";
        foreach (var l in cfg.Letters) Letters.Add(l);
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
            Cv          = string.IsNullOrWhiteSpace(CvPath) ? null : new FileEntry { Key = CvKey, Label = System.IO.Path.GetFileName(CvPath), Path = CvPath },
            Letters     = [.. Letters]
        };
        FileConfigService.Save(cfg);
        SaveStatus = $"Saved at {DateTime.Now:HH:mm:ss}";
    }
}
