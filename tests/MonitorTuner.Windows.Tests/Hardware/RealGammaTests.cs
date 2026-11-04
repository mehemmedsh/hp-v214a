using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Hardware;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;
using MonitorTuner.Services;
using Xunit.Abstractions;

namespace MonitorTuner.Windows.Tests.Hardware;

/// <summary>
/// 🔧 T302: gerçek GPU tablosu yazılıp geri alınır. Varsayılan atlanır (CI ve ajanlar çalıştırmaz); sadece kullanıcı
/// ortam değişkeniyle açar. Yol uygulamanınkiyle aynı: ColorPipeline → Güvenlik → WindowsGpuGammaTable.
/// </summary>
public class RealGammaTests(ITestOutputHelper output)
{
    /// <summary>MONITORTUNER_REAL_GAMMA=1: ekran 3 sn hafif sıcak olur, sonra Güvenlik eski tabloya döndürür.</summary>
    [RealMonitorFact("MONITORTUNER_REAL_GAMMA")]
    public void RealTable_WrittenAndRolledBack()
    {
        var (pipeline, safety, gpu, display) = Open();
        var before = gpu.Read(display);
        Assert.NotNull(before);
        output.WriteLine($"Ekran: {display.Value} · Önceki tablo birim mi: {before.Equals(GammaRamp.Identity)}");

        pipeline.SetLayer(ColorLayer.BlueLight, GammaRamps.Scale(new RgbGains(1, 0.9, 0.75)));
        Assert.True(pipeline.Apply(), "Windows tabloyu reddetti.");
        Assert.NotEqual(before, gpu.Read(display));
        Assert.False(pipeline.RestoreIfChangedExternally(), "Kendi yazması dış değişiklik sayıldı (Gece ışığı ya da f.lux açık mı?).");
        Thread.Sleep(TimeSpan.FromSeconds(3));

        safety.RestoreFactoryDefaults(); // ilk yazmadan önceki tabloya, boru hattı üzerinden
        Assert.Equal(before, gpu.Read(display));
        output.WriteLine("Tablo eski hâline döndü.");
    }

    /// <summary>MONITORTUNER_REAL_GAMMARANGE=1: GdiIcmGammaRange 256 olur (yönetici onayı), sonra eski hâline döner (ikinci onay).</summary>
    [RealMonitorFact("MONITORTUNER_REAL_GAMMARANGE")]
    public void RealGammaRange_SetAndRestored()
    {
        var (pipeline, safety, _, _) = Open();
        var range = new WindowsGdiIcmGammaRange();
        var before = range.Read();
        output.WriteLine($"Önce: {before?.ToString() ?? "değer yok"}");

        Assert.True(pipeline.EnsureFullGammaRange(), "Yönetici onayı verilmedi ya da yazılamadı.");
        Assert.Equal(ColorPipeline.FullGammaRange, range.Read());

        safety.CleanUpForUninstall(); // tablo birime, GdiIcmGammaRange eski hâline
        Assert.Equal(before, range.Read());
        output.WriteLine($"Sonra: {range.Read()?.ToString() ?? "değer yok"}");
    }

    private static (ColorPipeline Pipeline, SafetyLayer Safety, WindowsGpuGammaTable Gpu, DisplayId Display) Open()
    {
        var ddc = new WindowsDdcMonitor();
        var found = WindowsDdcMonitor.ListDisplays()
            .Where(d => ddc.ReadCapabilities(d)?.Contains("P21b", StringComparison.OrdinalIgnoreCase) == true).ToList();
        Assert.True(found.Count == 1, $"HP P21b G4 {found.Count} ekranda bulundu (1 olmalı): DDC/CI açık mı (OSD → Management)?");
        var display = found[0];

        var gpu = new WindowsGpuGammaTable();
        var range = new WindowsGdiIcmGammaRange();
        ColorPipeline? pipeline = null;
        var safety = new SafetyLayer(
            display, [], new SafetyHardware(ddc, gpu, new NoModes(), new NoNvidia(), range),
            () => pipeline!, new MemoryStore(), TimeProvider.System);
        pipeline = new ColorPipeline(display, gpu, range, new GammaTableCombiner(), safety);
        return (pipeline, safety, gpu, display);
    }

    // Hz ve NVIDIA bu testte okunmaz ve yazılmaz (Güvenlik "okunamadı" sayar, ilk açılış yedeğine almaz). DDC kodu verilmez.
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

    private sealed class MemoryStore : IFactoryBackupStore
    {
        private FactoryBackup? saved;
        public FactoryBackup? Load() => saved;
        public void Save(FactoryBackup backup) => saved = backup;
    }
}
