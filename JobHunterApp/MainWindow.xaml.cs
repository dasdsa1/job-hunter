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

    private SetupView?  _setupView;
    private SearchView? _searchView;
    private RunView?    _runView;

    public MainWindow()
    {
        InitializeComponent();
        _searchVm.StartRequested += OnStartRequested;
        MainTabs.SelectedIndex = 0;
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
            _ = _runVm.RunAsync(config);
    }
}
