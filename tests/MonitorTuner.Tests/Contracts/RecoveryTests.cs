using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;
using MonitorTuner.Services;
using MonitorTuner.Tests.Fakes;

namespace MonitorTuner.Tests.Contracts;

/// <summary>
/// Kurtarma sözleşmesi (02-MIMARI.md, T104): sahte sistem + gerçek Güvenlik ve renk boru hattı. Kurtarma dosyası ayrı bir
/// açılıştır: önceki oturumun (çökmüş uygulamanın) bıraktığı donanım ve ilk açılış yedeği aynı kalır, servisler yeniden kurulur.
/// </summary>
public class RecoveryTests
{
    private static readonly DisplayId Display = FakeHardware.Display;
    private static readonly GammaRamp Dim = GammaRamps.Scale(new RgbGains(0.3, 0.3, 0.3));

    /// <summary>GPU tablosu, GdiIcmGammaRange, dithering ve canlılık varsayılana döner.</summary>
    [Fact]
    public void ResetToDefaults_ResetsTableGammaRangeDitheringAndVibrance()
    {
        var (hw, store) = (new FakeHardware(76), new MemoryBackupStore(null));
        CrashedSession(hw, store);
        var modeBefore = hw.Modes.Current;

        var report = new Rig(hw, store).Recovery.ResetToDefaults();

        Assert.Equal(new RecoveryReport(true, true, true, true), report);
        Assert.Equal(GammaRamp.Identity, hw.Gpu.Read(Display));
        Assert.Null(hw.GammaRange.Read()); // kullanıcının eski hâli: değer yoktu
        Assert.Equal(FakeNvidiaSettings.DriverDefault, hw.Nvidia.Read(Display));
        // DDC'ye ve Hz'e dokunmaz.
        Assert.Empty(hw.Monitor.Writes);
        Assert.Equal(modeBefore, hw.Modes.Current);
        Assert.Single(hw.Modes.Switches); // sadece önceki oturumun geçişi
    }

    /// <summary>GdiIcmGammaRange Windows varsayılanına değil, kullanıcının uygulamadan önceki değerine döner.</summary>
    [Fact]
    public void ResetToDefaults_RestoresUsersOwnGammaRange()
    {
        var (hw, store) = (new FakeHardware(), new MemoryBackupStore(null));
        Assert.True(hw.GammaRange.Write(128, FakeRestorePoints.For(WriteTarget.GdiIcmGammaRange, Display)));
        CrashedSession(hw, store);
        Assert.Equal(ColorPipeline.FullGammaRange, hw.GammaRange.Read());

        Assert.True(new Rig(hw, store).Recovery.ResetToDefaults().GammaRangeRestored);
        Assert.Equal(128, hw.GammaRange.Read());
    }

    /// <summary>İlk açılış yedeği yoksa uygulama hiç yazmamıştır: GdiIcmGammaRange kullanıcınındır, dokunulmaz.</summary>
    [Fact]
    public void ResetToDefaults_WithoutFactoryBackup_LeavesGammaRangeAlone()
    {
        var hw = new FakeHardware();
        Assert.True(hw.GammaRange.Write(256, FakeRestorePoints.For(WriteTarget.GdiIcmGammaRange, Display)));

        Assert.True(new Rig(hw, new MemoryBackupStore(null)).Recovery.ResetToDefaults().GammaRangeRestored);
        Assert.Equal(256, hw.GammaRange.Read());
        Assert.Equal(1, hw.GammaRange.WriteCount);
    }

    /// <summary>Her şey zaten varsayılandaysa hiçbir şey yazılmaz.</summary>
    [Fact]
    public void ResetToDefaults_WhenAlreadyDefault_WritesNothing()
    {
        var hw = new FakeHardware();

        Assert.Equal(new RecoveryReport(true, true, true, true), new Rig(hw, new MemoryBackupStore(null)).Recovery.ResetToDefaults());
        Assert.Empty(hw.Gpu.Writes);
        Assert.Equal(0, hw.GammaRange.WriteCount);
        Assert.Equal(0, hw.Nvidia.WriteCount);
    }

    /// <summary>Güvenli modda NVIDIA sürücüsü yüklü değildir: tablo ve GdiIcmGammaRange yine döner, NVIDIA "yapılamadı".</summary>
    [Fact]
    public void ResetToDefaults_WithoutNvidiaDriver_ResetsTheRest()
    {
        var (hw, store) = (new FakeHardware(), new MemoryBackupStore(null));
        CrashedSession(hw, store);
        hw.Nvidia.Available = false;

        Assert.Equal(new RecoveryReport(true, true, false, false), new Rig(hw, store).Recovery.ResetToDefaults());
        Assert.Equal(GammaRamp.Identity, hw.Gpu.Read(Display));
        Assert.Null(hw.GammaRange.Read());
    }

    /// <summary>Yönetici onayı verilmezse GdiIcmGammaRange kalır ve bildirilir; diğerleri yine döner.</summary>
    [Fact]
    public void ResetToDefaults_AdminDeclines_ReportsGammaRangeOnly()
    {
        var (hw, store) = (new FakeHardware(), new MemoryBackupStore(null));
        CrashedSession(hw, store);
        hw.GammaRange.AdminApproves = false;

        Assert.Equal(new RecoveryReport(true, false, true, true), new Rig(hw, store).Recovery.ResetToDefaults());
        Assert.Equal(ColorPipeline.FullGammaRange, hw.GammaRange.Read());
    }

    /// <summary>Tablo okunamazsa yedeksiz yazılmaz (Güvenlik); bildirilir.</summary>
    [Fact]
    public void ResetToDefaults_TableUnreadable_IsNotWritten()
    {
        var (hw, store) = (new FakeHardware(), new MemoryBackupStore(null));
        CrashedSession(hw, store);
        var writes = hw.Gpu.Writes.Count;
        hw.Gpu.ReadFails = true;

        Assert.False(new Rig(hw, store).Recovery.ResetToDefaults().GpuTableReset);
        Assert.Equal(writes, hw.Gpu.Writes.Count);
    }

    /// <summary>Bir adımın hatası (ör. kayıt defteri istisnası) kalan adımları durdurmaz.</summary>
    [Fact]
    public void ResetToDefaults_StepThrows_OtherStepsStillRun()
    {
        var (hw, store) = (new FakeHardware(), new MemoryBackupStore(null));
        CrashedSession(hw, store);

        var report = new Rig(hw, store, new ThrowingGammaRange()).Recovery.ResetToDefaults();

        Assert.Equal(new RecoveryReport(true, false, true, true), report);
        Assert.Equal(FakeNvidiaSettings.DriverDefault, hw.Nvidia.Read(Display));
    }

    /// <summary>Kurtarmanın her yazması geri alınabilir: Güvenlik'ten geri alma noktası alır.</summary>
    [Fact]
    public void ResetToDefaults_EveryWriteHasRestorePoint_AndCanBeRolledBack()
    {
        var (hw, store) = (new FakeHardware(), new MemoryBackupStore(null));
        CrashedSession(hw, store);
        var rig = new Rig(hw, store);
        rig.Recovery.ResetToDefaults();

        var (_, point) = hw.Gpu.Writes[^1];
        rig.Safety.RollBack(point);
        Assert.Equal(Dim, hw.Gpu.Read(Display));
    }

    /// <summary>Önceki oturum: 76 Hz, GdiIcmGammaRange 256, koyu tablo, dithering açık, canlılık %65; sonra çöktü (kapanış yok).</summary>
    private static void CrashedSession(FakeHardware hw, MemoryBackupStore store)
    {
        var rig = new Rig(hw, store);
        var control = new NvidiaControl(Display, hw.Nvidia, rig.Safety, store);
        var pipeline = new ColorPipeline(Display, hw.Gpu, hw.GammaRange, new GammaTableCombiner(), rig.Safety, control);
        Assert.True(pipeline.EnsureFullGammaRange());
        pipeline.SetLayer(ColorLayer.Dimming, Dim);
        Assert.True(pipeline.Apply());
        Assert.True(control.SetVibrance(65));
        if (hw.Modes.ListModes(Display).FirstOrDefault(m => m.RefreshHz == 76) is { RefreshHz: 76 } mode)
            Assert.True(hw.Modes.SwitchMode(Display, mode, rig.Safety.CreateRestorePoint(new WriteRequest(WriteTarget.DisplayMode, Display))));
        Assert.Equal(DitheringMode.On, hw.Nvidia.Read(Display)!.Value.Dithering);
    }

    /// <summary>Kurtarma dosyasının kurduğu servisler: sahte donanım, gerçek Güvenlik ve renk boru hattı.</summary>
    private sealed class Rig
    {
        public Rig(FakeHardware hw, IFactoryBackupStore store, IGdiIcmGammaRange? gammaRange = null)
        {
            var range = gammaRange ?? hw.GammaRange;
            ColorPipeline? pipeline = null;
            Safety = new SafetyLayer(
                Display, [], new SafetyHardware(hw.Monitor, hw.Gpu, hw.Modes, hw.Nvidia, range), () => pipeline!, store, hw.Clock);
            pipeline = new ColorPipeline(Display, hw.Gpu, range, new GammaTableCombiner(), Safety);
            Recovery = new Recovery(Display, pipeline, range, hw.Nvidia, Safety, store);
        }

        public SafetyLayer Safety { get; }
        public Recovery Recovery { get; }
    }

    private sealed class MemoryBackupStore(FactoryBackup? saved) : IFactoryBackupStore
    {
        public FactoryBackup? Load() => saved;
        public void Save(FactoryBackup backup) => saved = backup;
    }

    private sealed class ThrowingGammaRange : IGdiIcmGammaRange
    {
        public int? Read() => ColorPipeline.FullGammaRange;
        public bool Write(int? value, RestorePoint restorePoint) => throw new UnauthorizedAccessException("kayıt defteri");
    }
}
