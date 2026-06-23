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
    [ObservableProperty] private bool _indeedApplyOnly       = true;

    // ICollectionView lets us re-filter via Refresh() without touching
    // the underlying collection — avoids the ItemsControl inconsistency
    public ICollectionView JobTitleSuggestions { get; }
    public ICollectionView LocationSuggestions { get; }
    public ICollectionView KeywordsSuggestions { get; }

    public event Action<SearchConfig>? StartRequested;

    public SearchViewModel()
    {
        _history = SearchHistoryService.Load();

        foreach (var s in _history.JobTitles) _jobTitleColl.Add(s);
        foreach (var s in _history.Locations) _locationColl.Add(s);
        foreach (var s in _history.Keywords)  _keywordsColl.Add(s);

        JobTitleSuggestions = CollectionViewSource.GetDefaultView(_jobTitleColl);
        LocationSuggestions = CollectionViewSource.GetDefaultView(_locationColl);
        KeywordsSuggestions = CollectionViewSource.GetDefaultView(_keywordsColl);

        JobTitleSuggestions.Filter = o => Matches(o, JobTitle);
        LocationSuggestions.Filter = o => Matches(o, Location);
        KeywordsSuggestions.Filter = o => Matches(o, Keywords);
    }

    // Refresh the filter whenever the bound text changes — single atomic Reset,
    // no add/remove events on the collection itself
    partial void OnJobTitleChanged(string value) => JobTitleSuggestions.Refresh();
    partial void OnLocationChanged(string value) => LocationSuggestions.Refresh();
    partial void OnKeywordsChanged(string value) => KeywordsSuggestions.Refresh();

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

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        var sites = new List<string>();
        if (UseLinkedIn) sites.Add("linkedin");
        if (UseIndeed)   sites.Add("indeed");

        var jt = JobTitle.Trim();
        var lo = Location.Trim();
        var kw = Keywords.Trim();

        SearchHistoryService.AddEntry(_history.JobTitles, jt);
        SearchHistoryService.AddEntry(_history.Locations, lo);
        SearchHistoryService.AddEntry(_history.Keywords,  kw);
        SearchHistoryService.Save(_history);

        // Push new entries into the backing collections without clearing them
        PushToFront(_jobTitleColl, _history.JobTitles);
        PushToFront(_locationColl, _history.Locations);
        PushToFront(_keywordsColl, _history.Keywords);

        StartRequested?.Invoke(new SearchConfig
        {
            JobTitle              = jt,
            Location              = lo,
            Keywords              = kw,
            Sites                 = sites,
            MinScore              = MinScore,
            MaxJobsPerSite        = MaxJobsPerSite,
            LinkedInEasyApplyOnly = LinkedInEasyApplyOnly,
            IndeedApplyOnly       = IndeedApplyOnly
        });
    }

    // Sync collection to match source by moving/inserting/trimming — no Clear()
    private static void PushToFront(ObservableCollection<string> coll, List<string> source)
    {
        if (source.Count == 0) return;
        var newest = source[0];
        var idx = coll.IndexOf(newest);
        if (idx == 0) return;                  // already at top
        if (idx > 0) coll.Move(idx, 0);        // move existing entry to front
        else          coll.Insert(0, newest);  // brand-new entry
        while (coll.Count > source.Count)
            coll.RemoveAt(coll.Count - 1);
    }
}
