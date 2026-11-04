using MonitorTuner.Core;
using MonitorTuner.Core.Profiles;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Services;

namespace MonitorTuner.Tests.Fakes;

/// <summary>
/// <see cref="IDdcControl"/>'ün sahte monitöre doğrudan bağlı test uygulaması (T305). Gerçek DDC katmanını (T301) taklit
/// etmez: izin listesi = profil ∩ monitörün bildirdiği kodlar, yasak kodlar (04/05/08, E6–FF) her koşulda Forbidden,
/// yazma sınırı yok (<see cref="Override"/> ile Throttled/Failed denenir). Geri alma noktasını kendisi üretir
/// (gerçekte Güvenlik katmanı).
/// </summary>
internal sealed class DirectDdcControl : IDdcControl
{
    private readonly FakeDdcMonitor monitor;
    private readonly DisplayId display;

    public DirectDdcControl(FakeDdcMonitor monitor, MonitorProfile profile, DisplayId? display = null)
    {
        this.monitor = monitor;
        this.display = display ?? FakeHardware.Display;
        var capabilities = monitor.ReadCapabilities(this.display);
        IsAvailable = capabilities is not null;
        // Yetenek dizesindeki kodlar (vcp(...)) ∩ profilde yazılabilir olanlar.
        var advertised = capabilities is null ? [] : ParseCodes(capabilities);
        Writable = [.. profile.WritableDdcCodes.Where(c => advertised.Contains(c) && !SafetyLimits.IsDdcWriteForbidden(c))];
    }

    public bool IsAvailable { get; }

    /// <summary>Testler kodu çıkarabilir (yazılamaz hâle getirir).</summary>
    public HashSet<byte> Writable { get; }

    /// <summary>null: gerçek sonuç. Throttled gibi bir değer dönerse monitöre hiçbir şey gitmez.</summary>
    public Func<byte, DdcWriteResult?>? Override { get; set; }

    IReadOnlySet<byte> IDdcControl.WritableCodes => Writable;

    public DdcValue? Read(byte code) => IsAvailable ? monitor.Read(display, code) : null;

    public DdcWriteResult Write(byte code, ushort value)
    {
        if (!IsAvailable) return DdcWriteResult.NoDdc;
        if (SafetyLimits.IsDdcWriteForbidden(code) || !Writable.Contains(code)) return DdcWriteResult.Forbidden;
        if (Override?.Invoke(code) is { } forced) return forced;
        var ok = monitor.Write(display, code, value, FakeRestorePoints.For(WriteTarget.Ddc, display, code));
        return ok ? DdcWriteResult.Written : DdcWriteResult.Failed;
    }

    private static HashSet<byte> ParseCodes(string capabilities)
    {
        var start = capabilities.IndexOf("vcp(", StringComparison.Ordinal) + 4;
        var codes = new HashSet<byte>();
        for (int i = start, depth = 0; i < capabilities.Length; i++)
        {
            var c = capabilities[i];
            if (c == '(') depth++;
            else if (c == ')') { if (depth == 0) break; depth--; }
            else if (depth == 0 && Uri.IsHexDigit(c) && i + 1 < capabilities.Length && Uri.IsHexDigit(capabilities[i + 1])
                     && (i == start || capabilities[i - 1] == ' '))
                codes.Add(Convert.ToByte(capabilities.Substring(i, 2), 16));
        }
        return codes;
    }
}
