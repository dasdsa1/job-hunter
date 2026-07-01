using System.Windows;
using System.Windows.Controls;
using JobHunterApp.Models;
using JobHunterApp.ViewModels;
using JobHunterApp.Views;

namespace JobHunterApp;

public partial class MainWindow : Window
{
    private readonly SearchViewModel _searchVm = new();
    private readonly RunViewModel    _runVm    = new();
    private SetupViewModel? _setupVm;

    private SetupView?  _setupView;
    private SearchView? _searchView;
    private RunView?    _runView;

    public MainWindow()
    {
        InitializeComponent();
        _searchVm.StartRequested += OnStartRequested;

        // First-run detection: if config.json doesn't exist, force Setup tab and disable others
        var isFirstRun = !File.Exists(AppPaths.ConfigFile);
        if (isFirstRun) MainTabs.SelectedIndex = 0;
        if (!isFirstRun)
        {
            _setupView ??= new SetupView();
            UpdateTabsState();
        }

        MainTabs.SelectionChanged += (s, e) => UpdateTabsState();
    }

    private void UpdateTabsState()
    {
        _setupVm ??= (_setupView as SetupView)?.DataContext as SetupViewModel;
        if (_setupVm is null) return;

        // Disable Search/Run tabs unless setup is complete
        if (MainTabs.Items.Count >= 3)
        {
            var searchTab = (TabItem)MainTabs.Items[1];
            var runTab    = (TabItem)MainTabs.Items[2];
            searchTab.IsEnabled = _setupVm.IsSetupComplete;
            runTab.IsEnabled    = _setupVm.IsSetupComplete;
        }
    }

    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MainTabs.SelectedItem is not TabItem tab) return;
        ContentArea.Content = (string)tab.Tag switch
        {
            "setup"  => _setupView  ??= new SetupView(),
            "search" => _searchView ??= new SearchView(_searchVm),
            "run"    => _runView    ??= new RunView(_runVm),
            _        => null
        };
    }

    private void OnStartRequested(SearchConfig config)
    {
        MainTabs.SelectedIndex = 2;
        if (!_runVm.IsRunning)
        {
            _runVm.RunAsync(config).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Dispatcher.Invoke(() =>
                        MessageBox.Show(
                            $"Run failed unexpectedly:\n\n{t.Exception?.InnerException?.Message ?? t.Exception?.Message}",
                            "Job Hunter", MessageBoxButton.OK, MessageBoxImage.Error));
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
