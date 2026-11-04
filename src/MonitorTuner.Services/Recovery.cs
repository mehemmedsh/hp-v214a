using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;

namespace MonitorTuner.Services;

/// <summary>
/// Kurtarma (T104): kurtarma dosyasının (<c>tools/recovery/Kurtarma.cmd</c> → <c>MonitorTuner.App.exe --kurtarma</c>) işi.
/// Sıra: GPU tablosu birime (renk boru hattı üzerinden), <c>GdiIcmGammaRange</c> ilk açılış yedeğindeki eski hâline,
/// dithering ve canlılık sürücü varsayılanına. DDC'ye ve Hz'e dokunmaz. Her yazma Güvenlik'ten geri alma noktası alır;
/// değer zaten yerindeyse yazmaz. Bir adım başarısız olsa da (güvenli modda NVIDIA sürücüsü yüklü değildir) diğerleri yapılır.
/// </summary>
public sealed class Recovery(
    DisplayId display, IColorPipeline pipeline, IGdiIcmGammaRange gammaRange, INvidiaSettings nvidia,
    ISafetyLayer safety, IFactoryBackupStore backups) : IRecovery
{
    /// <summary>Sürücü varsayılanı: dithering sürücüye bırakılmış, canlılık %50.</summary>
    public static readonly NvidiaState NvidiaDefaults = new(DitheringMode.DriverDefault, NvidiaControl.DriverDefaultVibrance);

    public RecoveryReport ResetToDefaults()
    {
        // Yedek ilk geri alma noktasından önce okunur: yoksa Güvenlik şimdiki hâli ilk açılış yedeği olarak kaydeder.
        var backup = Step(backups.Load, null);
        var table = Step(pipeline.ResetTable, false);
        var range = Step(() => RestoreGammaRange(backup), false); // tablo birimken (SafetyLayer.CleanUpForUninstall ile aynı sıra)
        var (dithering, vibrance) = Step(ResetNvidia, (false, false));
        return new RecoveryReport(table, range, dithering, vibrance);
    }

    private bool RestoreGammaRange(FactoryBackup? backup)
    {
        // Yedek yoksa uygulama hiç yazmamıştır (Güvenlik ilk yazmadan önce yedekler): değer kullanıcınındır.
        if (backup is null || gammaRange.Read() == backup.GammaRange) return true;
        var point = safety.CreateRestorePoint(new WriteRequest(WriteTarget.GdiIcmGammaRange, display));
        return gammaRange.Write(backup.GammaRange, point);
    }

    private (bool Dithering, bool Vibrance) ResetNvidia()
    {
        if (nvidia.Read(display) is not { } now) return (false, false); // sürücü yok (güvenli mod) ya da cevap vermiyor
        if (now != NvidiaDefaults)
            nvidia.ResetToDefaults(display, safety.CreateRestorePoint(new WriteRequest(WriteTarget.NvidiaSettings, display)));
        var after = nvidia.Read(display);
        return (after?.Dithering == NvidiaDefaults.Dithering, after?.Vibrance == NvidiaDefaults.Vibrance);
    }

    /// <summary>Son çare aracı: sürücü, kayıt defteri ya da yedek hatası o adımı "yapılamadı" yapar, kalanlar yine denenir.</summary>
    private static T Step<T>(Func<T> step, T failed)
    {
        try
        {
            return step();
        }
        catch (Exception)
        {
            return failed;
        }
    }
}
