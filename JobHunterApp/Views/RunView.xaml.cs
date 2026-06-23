using System.Collections.Specialized;
using System.Windows;
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

    private void CopyLog_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not RunViewModel vm || vm.Log.Count == 0) return;
        Clipboard.SetText(string.Join(Environment.NewLine, vm.Log));
    }
}
