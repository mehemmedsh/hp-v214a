using MonitorTuner.Core;

namespace MonitorTuner.Services;

/// <summary>Mola hatırlatıcısının kaydı (ayar deposu, "BreakReminder" bölümü).</summary>
public sealed record BreakReminderRecord(bool Enabled);

/// <summary>
/// Mola hatırlatıcısı (T324): 20-20-20 (her 20 dakikada 20 saniye, 6 metre uzağa bak). Varsayılan kapalı; kullanıcı açar.
/// Hiçbir ekran ayarına dokunmaz (Hz, parlaklık, renk): sadece "şimdi hatırlat" der, iletiyi arayüz gösterir.
/// </summary>
public interface IBreakReminder
{
    /// <summary>Açık mı; değişince ayar deposuna yazılır ve sayaç baştan başlar.</summary>
    bool Enabled { get; set; }

    /// <summary>
    /// Zamanlayıcı (birkaç saniyede bir) çağırır. true: şimdi hatırlat (bir kez; sonraki 20 dakika sonra).
    /// </summary>
    /// <param name="idle">Kullanıcının son klavye/fare girdisinden beri geçen süre.</param>
    /// <param name="paused">Film ya da oyun modu ekranda: hatırlatma araya girmez, sayaç beklemez (mod değişince yeni 20 dakika).</param>
    bool Tick(TimeSpan idle, bool paused);
}

/// <summary>
/// Mola hatırlatıcısı (T324). Etkin kullanım 20 dakikayı bulunca hatırlatır. 5 dakika boşta kalmak (kalkmak, uzağa bakmak) doğal
/// moladır: sayaç sıfırlanır. Tek iş parçacığından çağrılır (arayüz zamanlayıcısı ve Ayarlar sayfası).
/// </summary>
public sealed class BreakReminder : IBreakReminder
{
    public const string Section = "BreakReminder";

    /// <summary>20-20-20'nin ilk 20'si.</summary>
    public static readonly TimeSpan WorkPeriod = TimeSpan.FromMinutes(20);

    /// <summary>Bu kadar boşta kalınca mola verilmiş sayılır.</summary>
    public static readonly TimeSpan IdleReset = TimeSpan.FromMinutes(5);

    private readonly ISettingsStore _store;
    private readonly MonitorKey _key;
    private readonly TimeProvider _time;
    private bool? _enabled;
    private DateTimeOffset _activeSince;

    public BreakReminder(ISettingsStore store, MonitorKey key, TimeProvider time)
    {
        _store = store;
        _key = key;
        _time = time;
        _activeSince = time.GetUtcNow();
    }

    public bool Enabled
    {
        get => _enabled ??= _store.Read<BreakReminderRecord>(_key, Section)?.Enabled ?? false;
        set
        {
            if (Enabled == value) return;
            _enabled = value;
            _store.Write(_key, Section, new BreakReminderRecord(value));
            _activeSince = _time.GetUtcNow();
        }
    }

    public bool Tick(TimeSpan idle, bool paused)
    {
        var now = _time.GetUtcNow();
        if (!Enabled || paused || idle >= IdleReset)
        {
            _activeSince = now; // kapalı, film/oyun ya da mola: yeni 20 dakika geri döndükten sonra başlar
            return false;
        }

        if (now - _activeSince < WorkPeriod) return false;
        _activeSince = now;
        return true;
    }

    /// <summary>
    /// Ekrandaki mod film ya da oyun mu (sinema ve özel gruptaki Oyun): hatırlatma araya girmesin. Elle değiştirilmiş mod da sayılır.
    /// </summary>
    public static bool IsWatchingOrPlaying(ActiveMode? active, IReadOnlyList<ColorProfile> profiles)
    {
        if (active is null) return false;
        var profile = profiles.FirstOrDefault(p => string.Equals(p.Name, active.Name, StringComparison.OrdinalIgnoreCase));
        return profile is { Group: ProfileGroup.Cinema } or { Group: ProfileGroup.Special, Name: "Oyun" };
    }
}
