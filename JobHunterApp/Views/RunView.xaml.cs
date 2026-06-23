using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Threading;
using JobHunterApp.ViewModels;

namespace JobHunterApp.Views;

public partial class RunView : UserControl
{
    public RunView(RunViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // Both Log.Add (in AddLog) and ScrollIntoView run at Background priority (4),
        // which is below Render (7). WPF layout at Render always completes before the
        // next Background item fires, so VirtualizingStackPanel.MeasureChild never sees
        // a mid-add collection state → no more "ItemsControl inconsistent" crash.
        vm.Log.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems?.Count > 0)
            {
                var item = e.NewItems[0];
                Dispatcher.BeginInvoke(DispatcherPriority.Background,
                    () => LogList.ScrollIntoView(item));
            }
        };
    }
}
