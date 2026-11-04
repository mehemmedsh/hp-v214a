using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;

namespace MonitorTuner.Safety;

/// <summary>Güvenlik katmanının okuduğu ve geri alırken yazdığı donanım. GPU tablosu sadece okunur.</summary>
public sealed record SafetyHardware(
    IDdcMonitor Ddc, IGpuGammaTable GpuTable, IDisplayModes Modes, INvidiaSettings Nvidia, IGdiIcmGammaRange GammaRange);

/// <summary>İlk açılıştaki değerler; "fabrika ayarına dön" ve kaldırma buna döner. null alan: okunamadı, dokunulmaz.</summary>
/// <param name="GammaRange">null: kayıt defterinde değer yoktu (Windows varsayılanı).</param>
public sealed record FactoryBackup(
    IReadOnlyDictionary<byte, ushort> Ddc, GammaRamp? GpuTable, NvidiaState? Nvidia, int? GammaRange);

/// <summary>İlk açılış yedeğinin kalıcı yeri (ayar deposunun yedek klasörü, T102); uygulama yeniden açılınca yedek yeniden alınmaz.</summary>
public interface IFactoryBackupStore
{
    FactoryBackup? Load();

    void Save(FactoryBackup backup);
}

/// <summary>Güvenlik katmanı (T103). Tek ekran: uygulamanın monitörü.</summary>
/// <param name="ddcCodes">İlk açılışta yedeklenecek DDC kodları (profildeki yazılabilir kodlar).</param>
/// <param name="gpuTableOwner">Renk boru hattı; boru hattı da bu katmanı kullandığı için geç verilir.</param>
public sealed class SafetyLayer(
    DisplayId display,
    IReadOnlyCollection<byte> ddcCodes,
    SafetyHardware hardware,
    Func<IGpuTableOwner> gpuTableOwner,
    IFactoryBackupStore backupStore,
    TimeProvider time) : ISafetyLayer
{
    /// <summary>Saklanan en fazla geri alma noktası (bellek sınırı); onay bekleyenler hiç atılmaz.</summary>
    public const int MaxKept = 100;

    private sealed record Entry(RestorePoint Point, object? Backup);

    // Kilit sadece kayıtları korur. Geri alma yazmaları kilit dışında: boru hattı Restore içinde yeniden
    // CreateRestorePoint çağırır ve kendi kilidiyle çapraz beklemeye girmemeli.
    private readonly Lock gate = new();
    private readonly List<Entry> kept = [];
    private readonly Dictionary<Guid, ITimer> timers = [];
    private FactoryBackup? factory;

    public event Action<RestorePoint>? RolledBack;
    public event Action<RestorePoint>? RollBackFailed;

    public RestorePoint CreateRestorePoint(WriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Display != display)
            throw new ArgumentException($"Güvenlik katmanı sadece {display.Value} için geri alma noktası verir.", nameof(request));
        if ((request.Target == WriteTarget.Ddc) != request.DdcCode.HasValue)
            throw new ArgumentException("DDC kodu her DDC yazmasında ve sadece onda verilir.", nameof(request));
        if (request.DdcCode is { } code && SafetyLimits.IsDdcWriteForbidden(code))
            throw new ArgumentException($"DDC {code:X2} koduna yazılmaz (03-GUVENLIK.md → 3).", nameof(request));

        lock (gate)
        {
            var current = ReadCurrent(request);
            AddToFactoryBackup(request, current);
            var point = new RestorePoint(Guid.NewGuid(), request, time.GetUtcNow());
            kept.Add(new Entry(point, current));
            while (kept.Count > MaxKept)
            {
                var oldest = kept.FindIndex(e => !timers.ContainsKey(e.Point.Id));
                if (oldest < 0) break;
                kept.RemoveAt(oldest);
            }
            return point;
        }
    }

    public void RollBack(RestorePoint restorePoint)
    {
        ArgumentNullException.ThrowIfNull(restorePoint);
        Entry? entry;
        lock (gate) entry = Take(restorePoint);
        if (entry is null) throw new ArgumentException("Bilinmeyen ya da zaten geri alınmış geri alma noktası.", nameof(restorePoint));
        if (!TryApply(entry))
        {
            lock (gate) kept.Add(entry);
            throw new InvalidOperationException($"{restorePoint.Request.Target} geri alınamadı.");
        }
        RolledBack?.Invoke(restorePoint);
    }

    public void RequireConfirmation(RestorePoint restorePoint)
    {
        ArgumentNullException.ThrowIfNull(restorePoint);
        lock (gate)
        {
            if (!kept.Exists(e => e.Point == restorePoint))
                throw new ArgumentException("Bilinmeyen ya da zaten geri alınmış geri alma noktası.", nameof(restorePoint));
            if (timers.ContainsKey(restorePoint.Id)) throw new InvalidOperationException("Zaten onay bekliyor.");
            timers[restorePoint.Id] = time.CreateTimer(
                _ => OnConfirmationTimeout(restorePoint), null, SafetyLimits.ConfirmationTimeout, Timeout.InfiniteTimeSpan);
        }
    }

    public bool Confirm(RestorePoint restorePoint)
    {
        ArgumentNullException.ThrowIfNull(restorePoint);
        lock (gate)
        {
            if (!timers.Remove(restorePoint.Id, out var timer)) return false;
            timer.Dispose();
            return true;
        }
    }

    public void RestoreFactoryDefaults() => ReturnToFactory(uninstall: false);

    public void CleanUpForUninstall() => ReturnToFactory(uninstall: true);

    private void OnConfirmationTimeout(RestorePoint point)
    {
        Entry? entry;
        lock (gate)
        {
            if (!timers.ContainsKey(point.Id)) return; // bu arada onaylandı ya da elle geri alındı
            entry = Take(point);
        }
        if (entry is not null && TryApply(entry))
        {
            RolledBack?.Invoke(point);
            return;
        }
        if (entry is not null) lock (gate) kept.Add(entry); // elle tekrar denenebilir
        RollBackFailed?.Invoke(point);
    }

    /// <summary>Kaydı çıkarır, bekleyen onay süresini durdurur. Kilit tutulurken çağrılır.</summary>
    private Entry? Take(RestorePoint point)
    {
        if (timers.Remove(point.Id, out var timer)) timer.Dispose();
        var index = kept.FindIndex(e => e.Point == point);
        if (index < 0) return null;
        var entry = kept[index];
        kept.RemoveAt(index);
        return entry;
    }

    private object? ReadCurrent(WriteRequest r) => r.Target switch
    {
        WriteTarget.Ddc => (object?)hardware.Ddc.Read(r.Display, r.DdcCode!.Value)?.Current ?? throw Unreadable(r),
        WriteTarget.GpuGammaTable => hardware.GpuTable.Read(r.Display) ?? throw Unreadable(r),
        WriteTarget.DisplayMode => (object?)hardware.Modes.GetCurrentMode(r.Display) ?? throw Unreadable(r),
        WriteTarget.NvidiaSettings => (object?)hardware.Nvidia.Read(r.Display) ?? throw Unreadable(r),
        WriteTarget.GdiIcmGammaRange => hardware.GammaRange.Read(),
        _ => throw new ArgumentOutOfRangeException(nameof(r), r.Target, null),
    };

    private static InvalidOperationException Unreadable(WriteRequest r) =>
        new($"{r.Target} {r.DdcCode:X2} okunamadı; yedeksiz yazılmaz.");

    /// <summary>Yedekteki değeri yazar; değer zaten oradaysa yazmaz (monitör belleği yıpranmasın). Hata geri dönülemedi sayılır.</summary>
    private bool TryApply(Entry entry)
    {
        var (point, backup) = (entry.Point, entry.Backup);
        var d = point.Request.Display;
        try
        {
            switch (point.Request.Target)
            {
                case WriteTarget.Ddc:
                    var code = point.Request.DdcCode!.Value;
                    var value = (ushort)backup!;
                    return hardware.Ddc.Read(d, code)?.Current == value || hardware.Ddc.Write(d, code, value, point);
                case WriteTarget.GpuGammaTable:
                    var table = (GammaRamp)backup!;
                    if (Equals(hardware.GpuTable.Read(d), table)) return true;
                    gpuTableOwner().Restore(d, table);
                    return Equals(hardware.GpuTable.Read(d), table);
                case WriteTarget.DisplayMode:
                    var mode = (DisplayMode)backup!;
                    return hardware.Modes.GetCurrentMode(d) == mode || hardware.Modes.SwitchMode(d, mode, point);
                case WriteTarget.NvidiaSettings:
                    var state = (NvidiaState)backup!;
                    var now = hardware.Nvidia.Read(d);
                    var dithering = now?.Dithering == state.Dithering || hardware.Nvidia.SetDithering(d, state.Dithering, point);
                    var vibrance = now?.Vibrance == state.Vibrance || hardware.Nvidia.SetVibrance(d, state.Vibrance, point);
                    return dithering && vibrance;
                case WriteTarget.GdiIcmGammaRange:
                    var range = (int?)backup;
                    return hardware.GammaRange.Read() == range || hardware.GammaRange.Write(range, point);
                default:
                    return false;
            }
        }
        catch (Exception)
        {
            return false; // sürücü ya da boru hattı hatası; zamanlayıcıdan uygulamayı çökertmesin
        }
    }

    /// <summary>İlk açılış yedeği: depoda yoksa şimdi, ilk yazmadan önce alınır. Kilit tutulurken çağrılır.</summary>
    private FactoryBackup Factory()
    {
        if (factory is not null) return factory;
        factory = backupStore.Load();
        if (factory is null) backupStore.Save(factory = CaptureFactory());
        return factory;
    }

    /// <summary>İlk açılışta okunamayan bir hedefe ilk kez yazılacaksa yedeğe yazmadan önceki değeri eklenir.</summary>
    private void AddToFactoryBackup(WriteRequest request, object? current)
    {
        var f = Factory();
        var filled = request.Target switch
        {
            WriteTarget.Ddc when !f.Ddc.ContainsKey(request.DdcCode!.Value) =>
                f with { Ddc = new Dictionary<byte, ushort>(f.Ddc) { [request.DdcCode.Value] = (ushort)current! } },
            WriteTarget.GpuGammaTable when f.GpuTable is null => f with { GpuTable = (GammaRamp)current! },
            WriteTarget.NvidiaSettings when f.Nvidia is null => f with { Nvidia = (NvidiaState)current! },
            _ => f,
        };
        if (!ReferenceEquals(filled, f)) backupStore.Save(factory = filled);
    }

    private FactoryBackup CaptureFactory()
    {
        var ddc = new Dictionary<byte, ushort>();
        foreach (var code in ddcCodes.Where(c => !SafetyLimits.IsDdcWriteForbidden(c)))
            if (hardware.Ddc.Read(display, code) is { } value) ddc[code] = value.Current;
        return new FactoryBackup(ddc, hardware.GpuTable.Read(display), hardware.Nvidia.Read(display), hardware.GammaRange.Read());
    }

    private void ReturnToFactory(bool uninstall)
    {
        FactoryBackup f;
        lock (gate) f = Factory();
        var failed = new List<string>();

        foreach (var (code, value) in f.Ddc.OrderBy(p => p.Key)) Step($"DDC {code:X2}", WriteTarget.Ddc, value, code);
        var table = uninstall ? GammaRamp.Identity : f.GpuTable;
        if (table is not null) Step("GPU renk tablosu", WriteTarget.GpuGammaTable, table);
        if (f.Nvidia is { } nvidia) Step("NVIDIA dithering ve canlılık", WriteTarget.NvidiaSettings, nvidia);
        if (uninstall) Step("GdiIcmGammaRange", WriteTarget.GdiIcmGammaRange, f.GammaRange); // tablo sıfırlandıktan sonra

        if (failed.Count > 0) throw new InvalidOperationException("Geri dönülemeyenler: " + string.Join(", ", failed));

        void Step(string name, WriteTarget target, object? value, byte? code = null)
        {
            try
            {
                // Her yazma kendi geri alma noktasıyla: fabrika ayarına dönüş de geri alınabilir.
                var point = CreateRestorePoint(new WriteRequest(target, display, code));
                if (!TryApply(new Entry(point, value))) failed.Add(name);
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException)
            {
                failed.Add(name); // okunamadı ya da depodaki yedekte yasak kod var
            }
        }
    }
}
