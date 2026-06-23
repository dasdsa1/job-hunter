using System.Windows;
using JobHunterApp.Models;

namespace JobHunterApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppPaths.EnsureDirectories();
    }
}
