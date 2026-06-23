using System.Collections.Specialized;
using System.Windows.Controls;
using JobHunterApp.ViewModels;

namespace JobHunterApp.Views;

public partial class RunView : UserControl
{
    public RunView(RunViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // Auto-scroll log to bottom on new entries
        vm.Log.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
                LogList.ScrollIntoView(LogList.Items[^1]);
        };
    }
}
