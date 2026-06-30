using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JobHunterApp.Models;
using JobHunterApp.Services;

namespace JobHunterApp.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly SearchHistory _history;

    // Backing collections — never cleared/rebuilt, only targeted mutations
    private readonly ObservableCollection<string> _jobTitleColl = [];
    private readonly ObservableCollection<string> _locationColl = [];
    private readonly ObservableCollection<string> _keywordsColl = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string _jobTitle = "";

    [ObservableProperty] private string _location  = "Remote";
    [ObservableProperty] private string _keywords  = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private bool _useLinkedIn = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private bool _useIndeed = true;

    [ObservableProperty] private int  _minScore              = 6;
    [ObservableProperty] private int  _maxJobsPerSite        = 20;
    [ObservableProperty] private bool _linkedInEasyApplyOnly = true;
    [ObservableProperty] private bool _indeedApplyOnly       = false;
    [ObservableProperty] private bool _skipAppliedJobs       = true;

    public ICollectionView JobTitleSuggestions { get; }
    public ICollectionView LocationSuggestions { get; }
    public ICollectionView KeywordsSuggestions { get; }

    public event Action<SearchConfig>? StartRequested;

    public SearchViewModel()
    {
        _history = SearchHistoryService.Load();

        // ── Restore last-used field values and checkbox states ───────────────
        // Set backing fields directly to avoid triggering partial hooks during init
        var s = _history.LastState;
        _jobTitle              = s.JobTitle;
        _location              = s.Location;
        _keywords              = s.Keywords;
        _useLinkedIn           = s.UseLinkedIn;
        _useIndeed             = s.UseIndeed;
        _linkedInEasyApplyOnly = s.LinkedInEasyApplyOnly;
        _indeedApplyOnly       = s.IndeedApplyOnly;
        _minScore              = s.MinScore;
        _maxJobsPerSite        = s.MaxJobsPerSite;

        AppLogger.Info($"SearchViewModel: restored state — jt='{_jobTitle}' lo='{_location}' linkedin={_useLinkedIn} indeed={_useIndeed}");

        // ── Populate autocomplete collections ────────────────────────────────
        foreach (var h in _history.JobTitles) _jobTitleColl.Add(h);
        foreach (var h in _history.Locations) _locationColl.Add(h);
        foreach (var h in _history.Keywords)  _keywordsColl.Add(h);

        JobTitleSuggestions = CollectionViewSource.GetDefaultView(_jobTitleColl);
        LocationSuggestions = CollectionViewSource.GetDefaultView(_locationColl);
        KeywordsSuggestions = CollectionViewSource.GetDefaultView(_keywordsColl);

        // Filters use the restored field values from the start
        JobTitleSuggestions.Filter = o => Matches(o, JobTitle);
        LocationSuggestions.Filter = o => Matches(o, Location);
        KeywordsSuggestions.Filter = o => Matches(o, Keywords);
    }

    partial void OnJobTitleChanged(string value)
    {
        AppLogger.Info($"SearchViewModel: JobTitle → '{value}'");
        try { JobTitleSuggestions.Refresh(); }
        catch (Exception ex) { AppLogger.Exception("JobTitleSuggestions.Refresh", ex); }
    }

    partial void OnLocationChanged(string value)
    {
        try { LocationSuggestions.Refresh(); }
        catch (Exception ex) { AppLogger.Exception("LocationSuggestions.Refresh", ex); }
    }

    partial void OnKeywordsChanged(string value)
    {
        try { KeywordsSuggestions.Refresh(); }
        catch (Exception ex) { AppLogger.Exception("KeywordsSuggestions.Refresh", ex); }
    }

    private static bool Matches(object? item, string text) =>
        item is string s &&
        (string.IsNullOrWhiteSpace(text) ||
         s.Contains(text, StringComparison.OrdinalIgnoreCase));

    public string ValidationMessage
    {
        get
        {
            if (string.IsNullOrWhiteSpace(JobTitle))
                return "Enter a job title to search for.";
            if (!UseLinkedIn && !UseIndeed)
                return "Select at least one site (LinkedIn or Indeed).";
            return "";
        }
    }

    private bool CanStart =>
        !string.IsNullOrWhiteSpace(JobTitle) && (UseLinkedIn || UseIndeed);

    [RelayCommand]
    private void ClearAppliedHistory()
    {
        var store = AppliedJobsService.Load();
        store.Jobs.Clear();
        AppliedJobsService.Save(store);
        AppLogger.Info("SearchViewModel: applied jobs history cleared");
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        var sites = new List<string>();
        if (UseLinkedIn) sites.Add("linkedin");
        if (UseIndeed)   sites.Add("indeed");

        var jt = JobTitle.Trim();
        var lo = Location.Trim();
        var kw = Keywords.Trim();

        AppLogger.Info($"SearchViewModel: Start() — jt='{jt}' sites={string.Join(",", sites)}");

        // ── Persist current state ────────────────────────────────────────────
        _history.LastState = new SearchState
        {
            JobTitle              = jt,
            Location              = lo,
            Keywords              = kw,
            UseLinkedIn           = UseLinkedIn,
            UseIndeed             = UseIndeed,
            LinkedInEasyApplyOnly = LinkedInEasyApplyOnly,
            IndeedApplyOnly       = IndeedApplyOnly,
            MinScore              = MinScore,
            MaxJobsPerSite        = MaxJobsPerSite
        };

        SearchHistoryService.AddEntry(_history.JobTitles, jt);
        SearchHistoryService.AddEntry(_history.Locations, lo);
        SearchHistoryService.AddEntry(_history.Keywords,  kw);
        SearchHistoryService.Save(_history);

        try
        {
            PushToFront(_jobTitleColl, _history.JobTitles);
            PushToFront(_locationColl, _history.Locations);
            PushToFront(_keywordsColl, _history.Keywords);
        }
        catch (Exception ex) { AppLogger.Exception("SearchViewModel.PushToFront", ex); }

        StartRequested?.Invoke(new SearchConfig
        {
            JobTitle              = jt,
            Location              = lo,
            Keywords              = kw,
            Sites                 = sites,
            MinScore              = MinScore,
            MaxJobsPerSite        = MaxJobsPerSite,
            LinkedInEasyApplyOnly = LinkedInEasyApplyOnly,
            IndeedApplyOnly       = IndeedApplyOnly,
            SkipAppliedJobs       = SkipAppliedJobs
        });
    }

    private static void PushToFront(ObservableCollection<string> coll, List<string> source)
    {
        if (source.Count == 0) return;
        var newest = source[0];
        var idx = coll.IndexOf(newest);
        if (idx == 0) return;
        if (idx > 0) coll.Move(idx, 0);
        else         coll.Insert(0, newest);
        while (coll.Count > source.Count)
            coll.RemoveAt(coll.Count - 1);
    }
}
