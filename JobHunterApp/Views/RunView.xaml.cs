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

        // Use e.NewItems[0] (the added item itself) instead of LogList.Items[^1].
        // Accessing Items[^1] during CollectionChanged forces a layout/measure pass;
        // if another Dispatcher.Invoke fires during that measure (re-entrant), the
        // ListBox item count gets out of sync → InvalidOperationException.
        vm.Log.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems?.Count > 0)
                LogList.ScrollIntoView(e.NewItems[0]);
        };
    }
}
