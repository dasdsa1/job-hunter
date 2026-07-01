using System.Windows;
using System.Windows.Threading;

namespace JobHunterApp.Services;

public class WpfDispatcher : IDispatcher
{
    public void Invoke(Action action)
    {
        Application.Current.Dispatcher.Invoke(action);
    }

    public void Invoke(DispatcherPriority priority, Action action)
    {
        Application.Current.Dispatcher.BeginInvoke(priority, action);
    }

    public async Task InvokeAsync(Func<Task> action)
    {
        await Application.Current.Dispatcher.InvokeAsync(async () => await action());
    }
}
