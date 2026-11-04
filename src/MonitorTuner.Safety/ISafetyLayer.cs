using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;

namespace MonitorTuner.Safety;

/// <summary>
/// Güvenlik katmanı (T103): geri alma noktasını oluşturabilen tek yer. Her donanım ve sistem yazması
/// önce buradan bir <see cref="RestorePoint"/> alır. Zaman için <see cref="TimeProvider"/> kullanılır (sahte saat).
/// </summary>
public interface ISafetyLayer
{
    /// <summary>
    /// Hedefin şu anki değerini yedekler, sonra geri alma noktası verir. Yedek alınamazsa
    /// <see cref="InvalidOperationException"/>; yasak DDC kodu (04/05/08, E6–FF), başka ekran ya da eksik/fazla DDC kodu
    /// <see cref="ArgumentException"/>. İlk çağrıda, hiçbir yazmadan önce, ilk açılış yedeği alınır.
    /// </summary>
    RestorePoint CreateRestorePoint(WriteRequest request);

    /// <summary>
    /// Yazmayı yedekteki değere geri alır. GPU tablosunu kendisi yazmaz, <see cref="IGpuTableOwner"/>'dan ister.
    /// Bilinmeyen ya da zaten geri alınmış nokta <see cref="ArgumentException"/>; donanım geri dönmezse
    /// <see cref="InvalidOperationException"/> ve nokta tekrar denemek için kalır.
    /// </summary>
    void RollBack(RestorePoint restorePoint);

    /// <summary>Onay bekletir: <see cref="SafetyLimits.ConfirmationTimeout"/> içinde <see cref="Confirm"/> gelmezse geri alır.</summary>
    void RequireConfirmation(RestorePoint restorePoint);

    /// <summary>Değişiklik kalır. Süre dolduktan ya da elle geri alındıktan sonra gelen onay etkisizdir.</summary>
    void Confirm(RestorePoint restorePoint);

    /// <summary>Onay gelmediği için ya da elle geri alındığında.</summary>
    event Action<RestorePoint>? RolledBack;

    /// <summary>Onay gelmedi ama donanım geri dönmedi; arayüz kullanıcıya "görüntü gelmezse" adımlarını gösterir.</summary>
    event Action<RestorePoint>? RollBackFailed;

    /// <summary>"Fabrika ayarına dön": DDC, GPU tablosu, dithering ve canlılık ilk açılıştaki yedeğe döner. Hz'e dokunmaz.</summary>
    void RestoreFactoryDefaults();

    /// <summary>Kaldırma: DDC yedeğe, tablo sıfıra, GdiIcmGammaRange eski hâline, dithering ve canlılık ilk hâline. Hz'e dokunmaz.</summary>
    void CleanUpForUninstall();
}

/// <summary>GPU renk tablosunun sahibi (renk boru hattı). Güvenlik geri yüklemeyi bunun üzerinden ister.</summary>
public interface IGpuTableOwner
{
    void Restore(DisplayId display, GammaRamp table);
}
