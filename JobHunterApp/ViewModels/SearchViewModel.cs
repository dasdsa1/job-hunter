using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JobHunterApp.Models;

namespace JobHunterApp.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    [ObservableProperty] private string _jobTitle       = "";
    [ObservableProperty] private string _location       = "Remote";
    [ObservableProperty] private string _keywords       = "";
    [ObservableProperty] private bool   _useLinkedIn    = true;
    [ObservableProperty] private bool   _useIndeed      = true;
    [ObservableProperty] private int    _minScore       = 6;
    [ObservableProperty] private int    _maxJobsPerSite = 20;
    [ObservableProperty] private bool   _linkedInEasyApplyOnly = true;
    [ObservableProperty] private bool   _indeedApplyOnly       = true;

    public event Action<SearchConfig>? StartRequested;

    [RelayCommand]
    private void Start()
    {
        if (string.IsNullOrWhiteSpace(JobTitle)) return;

        var sites = new List<string>();
        if (UseLinkedIn) sites.Add("linkedin");
        if (UseIndeed)   sites.Add("indeed");
        if (sites.Count == 0) return;

        StartRequested?.Invoke(new SearchConfig
        {
            JobTitle       = JobTitle.Trim(),
            Location       = Location.Trim(),
            Keywords       = Keywords.Trim(),
            Sites          = sites,
            MinScore              = MinScore,
            MaxJobsPerSite        = MaxJobsPerSite,
            LinkedInEasyApplyOnly = LinkedInEasyApplyOnly,
            IndeedApplyOnly       = IndeedApplyOnly
        });
    }
}
