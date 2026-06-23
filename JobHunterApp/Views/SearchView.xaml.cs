using System.Windows.Controls;
using JobHunterApp.ViewModels;

namespace JobHunterApp.Views;

public partial class SearchView : UserControl
{
    public SearchView(SearchViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
