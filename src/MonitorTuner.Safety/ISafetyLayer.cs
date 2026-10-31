using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;

namespace MonitorTuner.Safety;

/// <summary>
/// Güvenlik katmanı (T103): geri alma noktasını oluşturabilen tek yer. Her donanım ve sistem yazması
/// önce buradan bir <see cref="RestorePoint"/> alır. Zaman için <see cref="TimeProvider"/> kullanılır (sahte saat).
/// </summary>
public interface ISafetyLayer
{
    /// <summary>Hedefin şu anki değerini yedekler, sonra geri alma noktası verir. Yedek alınamazsa hata fırlatır.</summary>
    RestorePoint CreateRestorePoint(WriteRequest request);

    /// <summary>Yazmayı yedekteki değere geri alır. GPU tablosunu kendisi yazmaz, <see cref="IGpuTableOwner"/>'dan ister.</summary>
    void RollBack(RestorePoint restorePoint);

    /// <summary>Onay bekletir: <see cref="SafetyLimits.ConfirmationTimeout"/> içinde <see cref="Confirm"/> gelmezse geri alır.</summary>
    void RequireConfirmation(RestorePoint restorePoint);

    void Confirm(RestorePoint restorePoint);

    /// <summary>Onay gelmediği için ya da elle geri alındığında.</summary>
    event Action<RestorePoint>? RolledBack;

    /// <summary>"Fabrika ayarına dön": ilk açılıştaki yedeğe döner.</summary>
    void RestoreFactoryDefaults();

    /// <summary>Kaldırma: DDC yedeğe, tablo sıfıra, GdiIcmGammaRange eski hâline, dithering ve canlılık ilk hâline.</summary>
    void CleanUpForUninstall();
}

/// <summary>GPU renk tablosunun sahibi (renk boru hattı). Güvenlik geri yüklemeyi bunun üzerinden ister.</summary>
public interface IGpuTableOwner
{
    void Restore(DisplayId display, GammaRamp table);
}
