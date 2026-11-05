namespace MonitorTuner.App.ViewModels;

/// <summary>
/// Olaydan başlatılan arka plan işi (T316). Görev beklenmeden bırakılırsa hatası kaybolurdu; burada beklenir ve hata arayüz
/// iş parçacığında yeniden atılır, böylece <c>DispatcherUnhandledException</c>'a (günlük + güvenli kapanış) ulaşır.
/// </summary>
public static class UiTask
{
    // async void bilerek: hata dağıtıcıya gitsin.
    public static async void Forget(this Task task) => await task;
}
