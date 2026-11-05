using MonitorTuner.Services;

namespace MonitorTuner.Windows.Tests.ViewModels;

/// <summary>
/// Elle ilerleyen sıra (T316): RunAsync işi bekletir, test <see cref="RunAll"/> deyince çalıştırır. Böylece "iş sürerken arayüz ne
/// gösteriyor" iş parçacığı olmadan, kesin sırayla test edilir. Kullanan test <see cref="SynchronizationContext"/>'i boşaltır ki
/// await'ten sonrası RunAll içinde, aynı iş parçacığında çalışsın.
/// </summary>
internal sealed class ManualLane : IWorkLane
{
    private readonly Queue<Action> _pending = new();

    public ManualLane() => SynchronizationContext.SetSynchronizationContext(null);

    public int Pending => _pending.Count;

    public Task<T> RunAsync<T>(Func<T> work)
    {
        var done = new TaskCompletionSource<T>();
        _pending.Enqueue(() =>
        {
            try
            {
                done.SetResult(work());
            }
            catch (Exception ex)
            {
                done.SetException(ex);
            }
        });
        return done.Task;
    }

    public T Run<T>(Func<T> work) => work();

    public void RunAll()
    {
        while (_pending.Count > 0) _pending.Dequeue()();
    }
}
