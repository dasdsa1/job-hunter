using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JobHunterApp.Models;

namespace JobHunterApp.ViewModels;

public partial class SearchViewModel : ObservableObject
{
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

    public event Action<SearchConfig>? StartRequested;

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

        StartRequested?.Invoke(new SearchConfig
        {
            JobTitle              = JobTitle.Trim(),
            Location              = Location.Trim(),
            Keywords              = Keywords.Trim(),
            Sites                 = sites,
            MinScore              = MinScore,
            MaxJobsPerSite        = MaxJobsPerSite,
            LinkedInEasyApplyOnly = LinkedInEasyApplyOnly,
            IndeedApplyOnly       = IndeedApplyOnly
        });
    }
}
