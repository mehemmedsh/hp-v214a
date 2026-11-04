using MonitorTuner.Core;
using MonitorTuner.Core.Profiles;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;

namespace MonitorTuner.Services;

/// <summary>Ayar deposundaki "RefreshRates" bölümü: Windows listesinde görülen onaylı Hz'ler ve uyarısı kabul edilen spec dışı Hz'ler.</summary>
public sealed record RefreshRateSettings(int[] SeenRatesHz, int[] AcceptedOutOfSpecHz)
{
    public static readonly RefreshRateSettings Empty = new([], []);
}

/// <summary>
/// Hz paneli (T209). Geçiş anlıktır (<see cref="IDisplayModes.SwitchMode"/>) ve Güvenlik'in 15 sn onayına bağlıdır;
/// onay süresi içinde gelirse mod Windows'a kaydedilir (<see cref="IDisplayModes.PersistMode"/>). Böylece uygulama onay
/// beklerken çökse bile yeniden başlatmada kayıtlı (önceki) mod gelir. Hz'i kendiliğinden hiç değiştirmez.
/// </summary>
public sealed class RefreshRatePanel : IRefreshRatePanel
{
    public const string SettingsSection = "RefreshRates";
    private const byte RefreshCode = 0xAE;

    /// <summary>01-OZELLIKLER.md → 2.1: 76 masaüstü, 75 video, 72 film, 60 orijinal, 50 EDID.</summary>
    public static readonly IReadOnlyList<int> DefaultApprovedRatesHz = [76, 75, 72, 60, 50];

    private readonly DisplayId display;
    private readonly MonitorProfile profile;
    private readonly IDisplayModes modes;
    private readonly IDdcControl ddc;
    private readonly ISafetyLayer safety;
    private readonly ISettingsStore store;
    private readonly MonitorKey key;
    private readonly Lock gate = new();
    private (RestorePoint Point, DisplayMode Mode)? pending;

    public RefreshRatePanel(
        DisplayId display, MonitorProfile profile, IDisplayModes modes, IDdcControl ddc, ISafetyLayer safety,
        ISettingsStore store, MonitorKey key)
    {
        (this.display, this.profile, this.modes, this.ddc, this.safety, this.store, this.key) =
            (display, profile, modes, ddc, safety, store, key);
        ApprovedRatesHz = [.. DefaultApprovedRatesHz.Where(hz => ProfileMode(hz) is not null)];
        safety.RolledBack += point => OnRolledBack(point, returned: true);
        safety.RollBackFailed += point => OnRolledBack(point, returned: false);
    }

    public IReadOnlyList<int> ApprovedRatesHz { get; }

    public int? CurrentRefreshHz => modes.GetCurrentMode(display)?.RefreshHz;

    public bool IsAwaitingConfirmation
    {
        get { lock (gate) return pending is not null; }
    }

    public event Action<bool>? Reverted;

    public IReadOnlyList<int> GetAvailableRatesHz()
    {
        var listed = ListedRates();
        return [.. ApprovedRatesHz.Where(listed.Contains)];
    }

    public IReadOnlyList<int> GetMissingRatesHz()
    {
        // Çözünürlükten bağımsız: masaüstü başka çözünürlükteyken (oyun) 1080p modları silinmiş sayılmaz.
        // Liste ya da şu anki mod okunamıyorsa (ekran uykuda, HDMI el sıkışması) karar verilmez, depoya yazılmaz.
        var all = modes.ListModes(display);
        if (all.Count == 0 || modes.GetCurrentMode(display) is null) return [];
        var listed = all.Select(m => m.RefreshHz).ToHashSet();
        var settings = Settings();
        var seen = settings.SeenRatesHz.Union(ApprovedRatesHz.Where(listed.Contains)).Order().ToArray();
        if (!seen.SequenceEqual(settings.SeenRatesHz)) store.Write(key, SettingsSection, settings with { SeenRatesHz = seen });
        return [.. ApprovedRatesHz.Where(hz => seen.Contains(hz) && !listed.Contains(hz))];
    }

    public ProfileMode? GetNvidiaValues(int refreshHz) =>
        ProfileMode(refreshHz) is { Source: ModeSource.CustomResolution } mode ? mode : null;

    public RefreshSwitchResult SwitchTo(int refreshHz, bool outOfSpecWarningAccepted = false)
    {
        if (!ApprovedRatesHz.Contains(refreshHz))
            throw new ArgumentOutOfRangeException(nameof(refreshHz), refreshHz, "Onaylı Hz modlarında yok.");

        lock (gate)
        {
            if (pending is not null) return RefreshSwitchResult.Busy;
            if (modes.GetCurrentMode(display) is not { } current) return RefreshSwitchResult.Failed;
            if (current.RefreshHz == refreshHz) return RefreshSwitchResult.Unchanged;

            var target = current with { RefreshHz = refreshHz };
            if (!modes.ListModes(display).Contains(target)) return RefreshSwitchResult.ModeMissing;

            if (profile.IsBeyondSpec(ProfileMode(refreshHz)!))
            {
                var settings = Settings();
                if (!settings.AcceptedOutOfSpecHz.Contains(refreshHz))
                {
                    if (!outOfSpecWarningAccepted) return RefreshSwitchResult.NeedsOutOfSpecWarning;
                    store.Write(key, SettingsSection,
                        settings with { AcceptedOutOfSpecHz = [.. settings.AcceptedOutOfSpecHz.Append(refreshHz).Order()] });
                }
            }

            RestorePoint point;
            try
            {
                point = safety.CreateRestorePoint(new WriteRequest(WriteTarget.DisplayMode, display));
            }
            catch (InvalidOperationException)
            {
                return RefreshSwitchResult.Failed; // şu anki mod okunamadı, yedeksiz geçilmez
            }

            if (!modes.SwitchMode(display, target, point))
            {
                // Windows reddetti; yarım kalmış bir değişiklik olduysa önceki moda dön.
                if (modes.GetCurrentMode(display) != current) TryRollBack(point);
                return RefreshSwitchResult.Failed;
            }

            safety.RequireConfirmation(point);
            pending = (point, target);
            return RefreshSwitchResult.AwaitingConfirmation;
        }
    }

    public RefreshConfirmResult Confirm()
    {
        (RestorePoint Point, DisplayMode Mode) waiting;
        lock (gate)
        {
            if (pending is not { } p) return RefreshConfirmResult.TooLate;
            // Güvenlik süre dolmadan onayı aldıysa geri dönüş artık olmaz; aldı değilse geri dönüş başladı.
            if (!safety.Confirm(p.Point)) return RefreshConfirmResult.TooLate;
            pending = null;
            waiting = p;
        }

        try
        {
            var save = safety.CreateRestorePoint(new WriteRequest(WriteTarget.DisplayMode, display));
            return modes.PersistMode(display, waiting.Mode, save) ? RefreshConfirmResult.Kept : RefreshConfirmResult.NotSaved;
        }
        catch (InvalidOperationException)
        {
            return RefreshConfirmResult.NotSaved;
        }
    }

    public int? ReadMonitorRefreshHz() =>
        ddc.Read(RefreshCode) is { } value && profile.FrequencyReadout.RefreshHz(value.Current) is { } hz
            ? (int)Math.Round(hz)
            : null;

    public bool? VerifyWithMonitor()
    {
        var windows = CurrentRefreshHz;
        var monitor = ReadMonitorRefreshHz();
        return windows is null || monitor is null ? null : windows == monitor;
    }

    private void OnRolledBack(RestorePoint point, bool returned)
    {
        lock (gate)
        {
            if (pending?.Point != point) return; // başka bir yazmanın geri dönüşü
            pending = null;
        }
        Reverted?.Invoke(returned);
    }

    private void TryRollBack(RestorePoint point)
    {
        try
        {
            safety.RollBack(point);
        }
        catch (InvalidOperationException)
        {
            // Güvenlik noktayı tekrar denemek için saklar; arayüz "görüntü gelmezse" adımlarını gösterir.
        }
    }

    private ProfileMode? ProfileMode(int refreshHz) => profile.Modes.FirstOrDefault(m => m.RefreshHz == refreshHz);

    private HashSet<int> ListedRates()
    {
        var current = modes.GetCurrentMode(display);
        return [.. modes.ListModes(display)
            .Where(m => current is null || (m.Width, m.Height) == (current.Value.Width, current.Value.Height))
            .Select(m => m.RefreshHz)];
    }

    private RefreshRateSettings Settings() => store.Read<RefreshRateSettings>(key, SettingsSection) ?? RefreshRateSettings.Empty;
}
