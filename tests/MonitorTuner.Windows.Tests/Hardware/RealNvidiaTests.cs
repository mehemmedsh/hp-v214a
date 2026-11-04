using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Hardware;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;
using MonitorTuner.Services;
using Xunit.Abstractions;

namespace MonitorTuner.Windows.Tests.Hardware;

/// <summary>
/// 🔧 T303: gerçek NVIDIA sürücüsünde dithering ve canlılık. Varsayılan atlanır; sadece kullanıcı ortam değişkeniyle açar.
/// Yol uygulamanınkiyle aynı: ColorPipeline → NvidiaControl → Güvenlik → WindowsNvidiaSettings.
/// </summary>
public class RealNvidiaTests(ITestOutputHelper output)
{
    /// <summary>
    /// MONITORTUNER_REAL_NVIDIA=1, ekranda tam ekran gri gradyan açıkken: 20 sn sıcak tablo + dithering açık (bant
    /// olmamalı), 20 sn aynı tablo dithering kapalı (karşılaştırma), 5 sn canlılık %75, sonra her şey eski hâline.
    /// </summary>
    [RealMonitorFact("MONITORTUNER_REAL_NVIDIA")]
    public void RealDitheringAndVibrance_SetAndRestored()
    {
        var display = FindMonitor();
        var nvidia = new WindowsNvidiaSettings();
        var gpu = new WindowsGpuGammaTable();
        var before = nvidia.Read(display);
        Assert.True(before is not null, "NVIDIA ayarları okunamadı: monitör RTX 3080'de mi, sürücü kurulu mu?");
        var table = gpu.Read(display);
        output.WriteLine($"Önce: dithering {before.Value.Dithering}, canlılık %{before.Value.Vibrance}");

        var range = new WindowsGdiIcmGammaRange();
        var store = new MemoryStore();
        ColorPipeline? pipeline = null;
        var safety = new SafetyLayer(
            display, [], new SafetyHardware(new WindowsDdcMonitor(), gpu, new NoModes(), nvidia, range),
            () => pipeline!, store, TimeProvider.System);
        var control = new NvidiaControl(display, nvidia, safety, store);
        pipeline = new ColorPipeline(display, gpu, range, new GammaTableCombiner(), safety, control);
        try
        {
            pipeline.SetLayer(ColorLayer.BlueLight, GammaRamps.Scale(new RgbGains(1, 0.75, 0.45)));
            Assert.True(pipeline.Apply(), "Windows tabloyu reddetti.");
            Assert.Equal(DitheringMode.On, nvidia.Read(display)?.Dithering);
            output.WriteLine("20 sn: dithering AÇIK. Gradyanda basamak olmamalı.");
            Thread.Sleep(TimeSpan.FromSeconds(20));

            var point = safety.CreateRestorePoint(new WriteRequest(WriteTarget.NvidiaSettings, display));
            Assert.True(nvidia.SetDithering(display, DitheringMode.Off, point));
            output.WriteLine("20 sn: dithering KAPALI (karşılaştırma). Basamaklar görünebilir.");
            Thread.Sleep(TimeSpan.FromSeconds(20));
            safety.RollBack(point);
            Assert.Equal(DitheringMode.On, nvidia.Read(display)?.Dithering);

            Assert.True(control.SetVibrance(75), "Canlılık yazılamadı.");
            Assert.Equal(75, nvidia.Read(display)?.Vibrance);
            output.WriteLine("5 sn: canlılık %75.");
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
        finally
        {
            pipeline.Shutdown();
            control.Shutdown();
            safety.RestoreFactoryDefaults(); // tablo, dithering ve canlılık ilk yazmadan önceki hâline
        }

        Assert.Equal(before, nvidia.Read(display));
        Assert.Equal(table, gpu.Read(display));
        output.WriteLine("Dithering, canlılık ve tablo eski hâline döndü.");
    }

    private static DisplayId FindMonitor()
    {
        var ddc = new WindowsDdcMonitor();
        var found = WindowsDdcMonitor.ListDisplays()
            .Where(d => ddc.ReadCapabilities(d)?.Contains("P21b", StringComparison.OrdinalIgnoreCase) == true).ToList();
        Assert.True(found.Count == 1, $"HP P21b G4 {found.Count} ekranda bulundu (1 olmalı): DDC/CI açık mı (OSD → Management)?");
        return found[0];
    }

    // Hz bu testte okunmaz ve yazılmaz; DDC kodu verilmez (DDC'ye yazılmaz).
    private sealed class NoModes : IDisplayModes
    {
        public IReadOnlyList<DisplayMode> ListModes(DisplayId display) => [];
        public DisplayMode? GetCurrentMode(DisplayId display) => null;
        public bool SwitchMode(DisplayId display, DisplayMode mode, RestorePoint restorePoint) => false;
        public bool PersistMode(DisplayId display, DisplayMode mode, RestorePoint restorePoint) => false;
    }

    private sealed class MemoryStore : IFactoryBackupStore
    {
        private FactoryBackup? saved;
        public FactoryBackup? Load() => saved;
        public void Save(FactoryBackup backup) => saved = backup;
    }
}
