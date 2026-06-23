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

        vm.Log.CollectionChanged += (_, _) =>
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () => LogBox.ScrollToEnd());
        };
    }

    private void CopyLog_Click(object sender, RoutedEventArgs e)
    {
        if (LogBox.Text.Length > 0)
            Clipboard.SetText(LogBox.Text);
    }
}
