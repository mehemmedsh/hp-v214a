using MonitorTuner.Core;
using MonitorTuner.Core.Profiles;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;

namespace MonitorTuner.Services;

/// <summary>
/// DDC katmanı (T301, 03-GUVENLIK.md → 3). Yetenek testi ilk kullanımda bir kez yapılır (<see cref="Probe"/> yeniler).
/// İzin listesi: profilde yazılabilir ∩ monitörün yetenek dizesi, 04/05/08 ve E6–FF hiçbir zaman. Değer profilin ve
/// monitörün sınırında olmalı; 14 (renk ön ayarı) sadece <see cref="MappingConfidence.Confirmed"/> eşlemeyle yazılır.
/// Monitördeki değer zaten isteneneyse yazılmaz. Yazma sınırı kod başınadır: <see cref="MinWriteInterval"/> içinde
/// ikinci yazma ve dakikada <see cref="MaxWritesPerMinute"/>'dan fazlası <see cref="DdcWriteResult.Throttled"/>.
/// Her yazma önce Güvenlik katmanından geri alma noktası alır. DDC hattına aynı anda tek istek gider (kilit).
/// </summary>
public sealed class DdcControl(DisplayId display, IDdcMonitor monitor, ISafetyLayer safety, MonitorProfile profile, TimeProvider time)
    : IDdcControl
{
    private const byte ColorPresetCode = 0x14;

    /// <summary>Aynı koda iki yazma arasındaki en kısa süre.</summary>
    public static readonly TimeSpan MinWriteInterval = TimeSpan.FromSeconds(1);

    /// <summary>Aynı koda son bir dakikada monitöre giden en fazla yazma (bellek yıpranmasın).</summary>
    public const int MaxWritesPerMinute = 10;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly Lock gate = new();
    private readonly Dictionary<byte, Queue<DateTimeOffset>> attempts = [];
    private IReadOnlySet<byte>? writable; // null: henüz test edilmedi
    private bool available;

    public bool IsAvailable
    {
        get { lock (gate) { EnsureProbed(); return available; } }
    }

    public IReadOnlySet<byte> WritableCodes
    {
        get { lock (gate) { EnsureProbed(); return writable!; } }
    }

    /// <summary>Yetenek testini yeniden yapar (monitör uykudan uyandı, kablo takıldı).</summary>
    public void Probe()
    {
        lock (gate)
        {
            var caps = monitor.ReadCapabilities(display);
            available = caps is not null;
            if (caps is null)
            {
                writable = new HashSet<byte>();
                return;
            }
            // Yetenek dizesinde liste yoksa profildeki kodlardan okunabilenler desteklenmiş sayılır.
            var supported = DdcCapabilities.ParseVcpCodes(caps);
            writable = profile.WritableDdcCodes
                .Where(c => !SafetyLimits.IsDdcWriteForbidden(c))
                .Where(c => supported?.Contains(c) ?? monitor.Read(display, c) is not null)
                .ToHashSet();
        }
    }

    public DdcValue? Read(byte code)
    {
        lock (gate)
        {
            EnsureProbed();
            return available ? monitor.Read(display, code) : null;
        }
    }

    public DdcWriteResult Write(byte code, ushort value)
    {
        lock (gate)
        {
            EnsureProbed();
            if (!available) return DdcWriteResult.NoDdc;
            if (SafetyLimits.IsDdcWriteForbidden(code) || !writable!.Contains(code) || !ProfileAllows(code, value))
                return DdcWriteResult.Forbidden;

            var current = monitor.Read(display, code);
            if (current is { } c && c.Maximum > 0 && value > c.Maximum) return DdcWriteResult.Forbidden;
            if (current?.Current == value) return DdcWriteResult.Written; // zaten yerinde: belleğe yazma yok

            var now = time.GetUtcNow();
            var recent = attempts.TryGetValue(code, out var q) ? q : attempts[code] = new Queue<DateTimeOffset>();
            while (recent.Count > 0 && now - recent.Peek() >= Window) recent.Dequeue();
            if (recent.Count >= MaxWritesPerMinute || (recent.Count > 0 && now - recent.Last() < MinWriteInterval))
                return DdcWriteResult.Throttled;

            RestorePoint point;
            try
            {
                point = safety.CreateRestorePoint(new WriteRequest(WriteTarget.Ddc, display, code));
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException)
            {
                return DdcWriteResult.Failed; // yedek alınamadı: yedeksiz yazılmaz
            }

            recent.Enqueue(now); // başarısız deneme de sayılır: cevap vermeyen monitör sıkıştırılmaz
            return monitor.Write(display, code, value, point) ? DdcWriteResult.Written : DdcWriteResult.Failed;
        }
    }

    private void EnsureProbed()
    {
        if (writable is null) Probe();
    }

    private bool ProfileAllows(byte code, ushort value)
    {
        var info = profile.DdcCodes.FirstOrDefault(c => c.Code == code);
        if (info is null) return false;
        if (info.PossibleValues.Count > 0 ? !info.PossibleValues.Contains(value) : info.Maximum is { } max && value > max)
            return false;
        return code != ColorPresetCode
            || profile.ColorPresetMap.Any(m => m.DdcValue == value && m.Confidence == MappingConfidence.Confirmed);
    }
}
