using Microsoft.Extensions.Time.Testing;
using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;

namespace MonitorTuner.Tests.Fakes;

/// <summary>
/// Kullanıcının sistemi, sahte (T003): RTX 3080'in HDMI'ında tek HP P21b G4, sahte saat. Mod değişince sahte
/// monitörün AE'si (dikey frekans) yeni Hz'i gösterir. Testler parçaları tek tek de kurabilir.
/// </summary>
internal sealed class FakeHardware
{
    public static readonly DisplayId Display = new(@"\\.\DISPLAY1");
    public static readonly DateTimeOffset Start = new(2026, 11, 4, 20, 0, 0, TimeSpan.Zero);

    /// <param name="refreshRates">Windows'un listesindeki Hz modları; orijinal 60 Hz her zaman vardır.</param>
    public FakeHardware(params int[] refreshRates) : this(FakeDdcMonitor.HpP21bG4(), refreshRates)
    {
    }

    public FakeHardware(FakeDdcMonitor monitor, params int[] refreshRates)
    {
        Monitor = monitor;
        var hz = refreshRates.Append(SafetyLimits.OriginalRefreshHz).Distinct().Order();
        Modes = new FakeDisplayModes(Display, hz.Select(r => new DisplayMode(1920, 1080, r)));
        Modes.ModeChanged += mode => Monitor.SetMonitorValue(0xAE, (ushort)(mode.RefreshHz * 100));
    }

    public FakeTimeProvider Clock { get; } = new(Start);
    public FakeDdcMonitor Monitor { get; }
    public FakeDisplayModes Modes { get; }
    public FakeGpuGammaTable Gpu { get; } = new(Display);
    public FakeNvidiaSettings Nvidia { get; } = new(Display);
    public FakeGdiIcmGammaRange GammaRange { get; } = new();
    public FakeDisplayEnumerator Displays { get; } = new(
        new ConnectedDisplay(Display, SampleEdid.HpP21bG4, ConnectionType.Hdmi, "HDMI", "NVIDIA GeForce RTX 3080"));
}

/// <summary>Örnek EDID: kullanıcının ünitesinden alınan orijinal (<c>donanim/hp-p21b-g4/edid-original.bin</c>).</summary>
internal static class SampleEdid
{
    private static readonly Lazy<byte[]> Recorded = new(() =>
        File.ReadAllBytes(Path.Combine(RepoRoot.Find(), "donanim", "hp-p21b-g4", "edid-original.bin")));

    /// <summary>Her çağrıda yeni kopya; testler birbirinin EDID'sini bozamaz.</summary>
    public static byte[] HpP21bG4 => (byte[])Recorded.Value.Clone();
}

/// <summary>Bağlı ekranlar; testler <see cref="Connected"/>'a VGA'daki ya da ikinci bir monitör ekleyebilir.</summary>
internal sealed class FakeDisplayEnumerator(params ConnectedDisplay[] displays) : IDisplayEnumerator
{
    public List<ConnectedDisplay> Connected { get; } = [.. displays];

    public IReadOnlyList<ConnectedDisplay> EnumerateDisplays() => Connected.ToList();
}

/// <summary>Testlerin geri alma noktası (gerçekte sadece Güvenlik oluşturur) ve sahtelerin ortak kontrolü.</summary>
internal static class FakeRestorePoints
{
    public static RestorePoint For(WriteTarget target, DisplayId display, byte? ddcCode = null, DateTimeOffset? at = null) =>
        new(Guid.NewGuid(), new WriteRequest(target, display, ddcCode), at ?? FakeHardware.Start);

    /// <summary>Başka hedef, ekran ya da DDC kodu için alınmış geri alma noktasıyla yazma bir hatadır (yedek yanlış olur).</summary>
    /// <param name="display">null: hedef ekrana bağlı değil (kayıt defteri).</param>
    public static void Check(RestorePoint restorePoint, WriteTarget target, DisplayId? display, byte? ddcCode = null)
    {
        ArgumentNullException.ThrowIfNull(restorePoint);
        var r = restorePoint.Request;
        if (r.Target != target || (display is { } d && r.Display != d) || r.DdcCode != ddcCode)
            throw new InvalidOperationException(
                $"Geri alma noktası ({r.Target}, {r.Display.Value}, {r.DdcCode:X2}) bu yazma için değil: {target}, {display?.Value}, {ddcCode:X2}");
    }
}
