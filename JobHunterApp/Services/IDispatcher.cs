using System.Windows.Threading;

namespace JobHunterApp.Services;

public interface IDispatcher
{
    void Invoke(Action action);
    void Invoke(DispatcherPriority priority, Action action);
    Task InvokeAsync(Func<Task> action);
}
