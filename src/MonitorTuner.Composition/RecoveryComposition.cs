using MonitorTuner.Core;
using MonitorTuner.Hardware;
using MonitorTuner.Safety;
using MonitorTuner.Services;

namespace MonitorTuner.Composition;

/// <summary>Bir ekranın kurtarma sonucu.</summary>
public sealed record DisplayRecovery(DisplayId Display, RecoveryReport Report);

/// <summary>
/// Kurtarma dosyasının (<c>tools/recovery</c>, T104) bağlama kökü: gerçek donanım, ilk açılış yedeği
/// (<c>%AppData%\MonitorTuner\backups</c>) sadece okunur. Uygulamanın kendisi açılmaz.
/// </summary>
public static class RecoveryComposition
{
    /// <summary>Masaüstündeki her ekranda <see cref="IRecovery.ResetToDefaults"/>; GdiIcmGammaRange tek kayıt, ilk ekranda döner.</summary>
    public static IReadOnlyList<DisplayRecovery> ResetAllDisplays()
    {
        var hardware = new SafetyHardware(
            new WindowsDdcMonitor(), new WindowsGpuGammaTable(), new WindowsDisplayModes(), new WindowsNvidiaSettings(),
            new WindowsGdiIcmGammaRange());
        var store = new JsonFactoryBackupStore(JsonFactoryBackupStore.DefaultFolder());
        return [.. WindowsDisplayList.ListActive()
            .Select(d => new DisplayRecovery(d, Recovery.Create(d, hardware, store, TimeProvider.System).ResetToDefaults()))];
    }

    /// <summary>İlk açılış yedeği var mı (yoksa uygulama GdiIcmGammaRange'e hiç yazmamıştır). Okunamazsa false.</summary>
    public static bool HasFactoryBackup()
    {
        try
        {
            return new JsonFactoryBackupStore(JsonFactoryBackupStore.DefaultFolder()).Load() is not null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
