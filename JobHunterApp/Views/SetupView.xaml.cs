using System.Windows.Controls;
using JobHunterApp.ViewModels;

namespace JobHunterApp.Views;

public partial class SetupView : UserControl
{
    public SetupView()
    {
        InitializeComponent();
        DataContext = new SetupViewModel();
    }
}
