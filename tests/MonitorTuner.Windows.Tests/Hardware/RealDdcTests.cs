using System.IO;
using MonitorTuner.Core;
using MonitorTuner.Core.Profiles;
using MonitorTuner.Hardware;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;
using MonitorTuner.Services;
using MonitorTuner.Tests;
using Xunit.Abstractions;

namespace MonitorTuner.Windows.Tests.Hardware;

/// <summary>
/// 🔧 T301: gerçek monitörde DDC okuma ve tek parlaklık yazması. Varsayılan atlanır (CI ve ajanlar çalıştırmaz);
/// sadece kullanıcı ortam değişkeniyle açar. Yol uygulamanınkiyle aynı: DdcControl → Güvenlik → WindowsDdcMonitor.
/// </summary>
public class RealDdcTests(ITestOutputHelper output)
{
    private const byte Brightness = 0x10;

    /// <summary>MONITORTUNER_REAL_DDC=1: okur, yazmaz.</summary>
    [RealMonitorFact("MONITORTUNER_REAL_DDC")]
    public void RealMonitor_Read()
    {
        var (ddc, display, profile) = Open();
        output.WriteLine($"Ekran: {display.Value}");
        output.WriteLine($"Yazılabilir kodlar: {string.Join(" ", ddc.WritableCodes.Order().Select(c => c.ToString("X2")))}");
        foreach (byte code in new byte[] { 0x10, 0x12, 0x14, 0x16, 0x18, 0x1A, 0x87, 0x0C })
            output.WriteLine($"{code:X2}: {Describe(ddc.Read(code))}");

        var ae = ddc.Read(0xAE);
        var ac = ddc.Read(0xAC);
        var hz = ae is { } v ? profile.FrequencyReadout.RefreshHz(v.Current) : null;
        var horizontal = ac is { } h ? profile.FrequencyReadout.HorizontalHz(h.Current) : null;
        output.WriteLine($"AE: {Describe(ae)} → {hz} Hz · AC: {Describe(ac)} → {horizontal} Hz (düzeltilmiş)");

        Assert.Contains(Brightness, ddc.WritableCodes);
        Assert.NotNull(ddc.Read(Brightness));
        Assert.InRange(hz ?? 0, 49, 77);
        Assert.NotNull(horizontal);
    }

    /// <summary>MONITORTUNER_DDC_BRIGHTNESS=&lt;0–100&gt;: parlaklığa tek yazma, sonra geri okur.</summary>
    [RealMonitorFact("MONITORTUNER_DDC_BRIGHTNESS")]
    public void RealMonitor_WriteBrightnessOnce()
    {
        var value = ushort.Parse(Environment.GetEnvironmentVariable("MONITORTUNER_DDC_BRIGHTNESS")!);
        Assert.InRange(value, 0, 100);
        var (ddc, _, _) = Open();
        var before = ddc.Read(Brightness);
        output.WriteLine($"Önce: {Describe(before)}");
        Assert.True(before?.Current != value, "Şu anki parlaklıktan farklı bir değer ver; aynı değer yazılmaz.");

        var result = ddc.Write(Brightness, value);
        var after = ddc.Read(Brightness);
        output.WriteLine($"Yazma: {result} · Sonra: {Describe(after)}");

        Assert.Equal(DdcWriteResult.Written, result);
        Assert.Equal(value, after?.Current);
    }

    private static (DdcControl Ddc, DisplayId Display, MonitorProfile Profile) Open()
    {
        var profile = new MonitorProfileReader().Read(File.ReadAllText(Path.Combine(RepoRoot.Find(), "profiles", "hp-p21b-g4.json")));
        var monitor = new WindowsDdcMonitor();
        var found = WindowsDdcMonitor.ListDisplays()
            .Where(d => monitor.ReadCapabilities(d)?.Contains("P21b", StringComparison.OrdinalIgnoreCase) == true).ToList();
        Assert.True(found.Count == 1, $"HP P21b G4 {found.Count} ekranda bulundu (1 olmalı): DDC/CI açık mı (OSD → Management)?");
        var display = found[0];

        var safety = new SafetyLayer(
            display, [.. profile.WritableDdcCodes], new SafetyHardware(monitor, new NoGpu(), new NoModes(), new NoNvidia(), new NoGammaRange()),
            () => throw new NotSupportedException(), new MemoryStore(), TimeProvider.System);
        return (new DdcControl(display, monitor, safety, profile, TimeProvider.System), display, profile);
    }

    private static string Describe(DdcValue? v) => v is { } x ? $"{x.Current} (en fazla {x.Maximum})" : "okunamadı";

    // DDC dışındaki donanım bu testte okunmaz ve yazılmaz (Güvenlik "okunamadı" sayar, ilk açılış yedeğine almaz).
    private sealed class NoGpu : IGpuGammaTable
    {
        public GammaRamp? Read(DisplayId display) => null;
        public bool Write(DisplayId display, GammaRamp table, RestorePoint restorePoint) => false;
    }

    private sealed class NoModes : IDisplayModes
    {
        public IReadOnlyList<DisplayMode> ListModes(DisplayId display) => [];
        public DisplayMode? GetCurrentMode(DisplayId display) => null;
        public bool SwitchMode(DisplayId display, DisplayMode mode, RestorePoint restorePoint) => false;
    }

    private sealed class NoNvidia : INvidiaSettings
    {
        public NvidiaState? Read(DisplayId display) => null;
        public bool SetDithering(DisplayId display, DitheringMode mode, RestorePoint restorePoint) => false;
        public bool SetVibrance(DisplayId display, int vibrance, RestorePoint restorePoint) => false;
        public bool ResetToDefaults(DisplayId display, RestorePoint restorePoint) => false;
    }

    private sealed class NoGammaRange : IGdiIcmGammaRange
    {
        public int? Read() => null;
        public bool Write(int? value, RestorePoint restorePoint) => false;
    }

    private sealed class MemoryStore : IFactoryBackupStore
    {
        private FactoryBackup? saved;
        public FactoryBackup? Load() => saved;
        public void Save(FactoryBackup backup) => saved = backup;
    }
}

/// <summary>Ortam değişkeni yoksa atlanır: gerçek monitöre dokunan test sadece kullanıcı açınca çalışır.</summary>
public sealed class RealMonitorFactAttribute : FactAttribute
{
    public RealMonitorFactAttribute(string variable)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable)))
            Skip = $"🔧 gerçek monitör: {variable} verilmedi";
    }
}
