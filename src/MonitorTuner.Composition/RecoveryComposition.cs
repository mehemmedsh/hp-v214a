using MonitorTuner.Color;
using MonitorTuner.Hardware;
using MonitorTuner.Safety;
using MonitorTuner.Services;

namespace MonitorTuner.Composition;

/// <summary>
/// Kurtarma dosyasının (T104) bağlaması: gerçek donanım + Güvenlik + renk boru hattı + <see cref="Recovery"/>. Uygulamanın
/// açılış bağlamasından (T314) ayrıdır; ayar sihirbazı, profil ve tepsi kurulmaz. Ekranlar Windows'un etkin ekranlarıdır
/// (DDC gerekmez; güvenli modda da bulunur). İlk açılış yedeği uygulamanınkiyle aynı dosyadır.
/// </summary>
public static class RecoveryComposition
{
    /// <summary>Etkin her ekran için bir kurtarma; ekran bulunamazsa boş.</summary>
    public static IReadOnlyList<IRecovery> ForActiveDisplays()
    {
        var backups = new FactoryBackupFile(new JsonSettingsStore(JsonSettingsStore.DefaultRoot(), TimeProvider.System).BackupFolder);
        var (ddc, gpu, modes) = (new WindowsDdcMonitor(), new WindowsGpuGammaTable(), new WindowsDisplayModes());
        var (nvidia, range) = (new WindowsNvidiaSettings(), new WindowsGdiIcmGammaRange());

        return [.. WindowsDdcMonitor.ListDisplays().Select(display =>
        {
            ColorPipeline? pipeline = null;
            // DDC kodu verilmez: kurtarma DDC'ye yazmaz; yedeğe kodlar uygulamanın ilk yazmasında eklenir (T103).
            var safety = new SafetyLayer(
                display, [], new SafetyHardware(ddc, gpu, modes, nvidia, range), () => pipeline!, backups, TimeProvider.System);
            pipeline = new ColorPipeline(display, gpu, range, new GammaTableCombiner(), safety);
            return (IRecovery)new Recovery(display, pipeline, range, nvidia, safety, backups);
        })];
    }
}
