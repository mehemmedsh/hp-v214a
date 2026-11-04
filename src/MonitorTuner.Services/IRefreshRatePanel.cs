using MonitorTuner.Core.Profiles;

namespace MonitorTuner.Services;

/// <summary>
/// <see cref="Unchanged"/>: zaten o Hz'de, yazılmadı. <see cref="Busy"/>: önceki geçiş hâlâ onay bekliyor.
/// <see cref="ModeMissing"/>: Windows listesinde yok (NVIDIA'ya eklenmeli: <see cref="IRefreshRatePanel.GetNvidiaValues"/>).
/// </summary>
public enum RefreshSwitchResult { AwaitingConfirmation, NeedsOutOfSpecWarning, ModeMissing, Unchanged, Busy, Failed }

/// <summary>
/// <see cref="Kept"/>: mod kalır ve Windows'a kaydedildi. <see cref="NotSaved"/>: mod kalır ama kaydedilemedi (yeniden
/// başlatınca önceki Hz gelir). <see cref="TooLate"/>: bekleyen geçiş yok ya da 15 sn dolmuş, önceki moda dönülüyor.
/// </summary>
public enum RefreshConfirmResult { Kept, NotSaved, TooLate }

/// <summary>
/// Hz paneli (T209): onaylı modlar arasında sadece kullanıcı isteğiyle geçiş. Her geçiş 15 sn onay bekler;
/// gelmezse önceki moda dönülür (Güvenlik). Mod Windows'a sadece onaydan sonra kaydedilir.
/// </summary>
public interface IRefreshRatePanel
{
    /// <summary>Onaylı modlar (76, 75, 72, 60, 50), profilde olanlar.</summary>
    IReadOnlyList<int> ApprovedRatesHz { get; }

    /// <summary>Windows'un şu anki Hz'i. null: okunamadı.</summary>
    int? CurrentRefreshHz { get; }

    bool IsAwaitingConfirmation { get; }

    /// <summary>Onay gelmeyen geçiş geri alındığında: true önceki moda dönüldü, false dönülemedi ("görüntü gelmezse" adımları).</summary>
    event Action<bool>? Reverted;

    /// <summary>Onaylı olup Windows listesinde olanlar.</summary>
    IReadOnlyList<int> GetAvailableRatesHz();

    /// <summary>
    /// Onaylı olup daha önce Windows listesinde görülen ama artık olmayanlar (sürücü güncellemesi sildi). Hiç eklenmemiş
    /// mod uyarı vermez. Çözünürlükten bağımsızdır (bütün modlara bakar). Her çağrı listede görülen onaylı modları ayar
    /// deposuna kaydeder; liste ya da şu anki mod okunamazsa boş döner ve depoya yazmaz.
    /// </summary>
    IReadOnlyList<int> GetMissingRatesHz();

    /// <summary>NVIDIA özel çözünürlüğüne girilecek değerler (profilden). null: EDID'de hazır mod ya da profilde yok.</summary>
    ProfileMode? GetNvidiaValues(int refreshHz);

    /// <param name="outOfSpecWarningAccepted">Spec dışı (72 Hz üstü) ilk geçişte kullanıcı uyarıyı kabul etti mi.</param>
    /// <exception cref="ArgumentOutOfRangeException">Onaylı modlarda olmayan Hz.</exception>
    RefreshSwitchResult SwitchTo(int refreshHz, bool outOfSpecWarningAccepted = false);

    /// <summary>Kullanıcı "görüntü geldi" dedi.</summary>
    RefreshConfirmResult Confirm();

    /// <summary>Monitörün gördüğü Hz (DDC AE). null: okunamadı.</summary>
    int? ReadMonitorRefreshHz();

    /// <summary>Monitörün gördüğü Hz Windows'unkiyle aynı mı. null: biri okunamadı (DDC yok).</summary>
    bool? VerifyWithMonitor();
}
