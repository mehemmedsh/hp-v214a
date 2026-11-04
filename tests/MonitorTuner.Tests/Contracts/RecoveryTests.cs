using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;
using MonitorTuner.Services;
using MonitorTuner.Tests.Fakes;

namespace MonitorTuner.Tests.Contracts;

/// <summary>Kurtarma sözleşmesi (02-MIMARI.md, T104): sahte sistemde gerçek Güvenlik katmanı ve renk boru hattıyla.</summary>
public class RecoveryTests
{
    private static readonly DisplayId Display = FakeHardware.Display;
    private static readonly GammaRamp Dark = GammaRamps.Scale(new RgbGains(0.15, 0.12, 0.08));

    /// <summary>
    /// Çökmüş uygulamanın bıraktığı hâl (koyu ve sıcak tablo, dithering açık, canlılık %70, GdiIcmGammaRange 256) varsayılana
    /// döner: birim tablo, sürücü varsayılanı, GdiIcmGammaRange ilk açılış yedeğindeki değer (null: değer yoktu, silinir).
    /// DDC'ye ve Hz'e dokunulmaz; ikinci çalıştırma hiçbir şey yazmaz.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(128)]
    public void ResetToDefaults_ResetsTableGammaRangeDitheringAndVibrance(int? originalGammaRange)
    {
        var rig = new Rig(originalGammaRange);
        rig.LeaveCrashedState();

        Assert.Equal(new RecoveryReport(true, true, true, true), rig.Recovery().ResetToDefaults());

        Assert.Equal(GammaRamp.Identity, rig.Hw.Gpu.Read(Display));
        Assert.Equal(originalGammaRange, rig.Hw.GammaRange.Read());
        Assert.Equal(FakeNvidiaSettings.DriverDefault, rig.Hw.Nvidia.Read(Display));
        Assert.Equal((1, 1, 1), rig.Writes()); // her biri tek yazma, kendi geri alma noktasıyla (sahteler hedefi denetler)
        Assert.Empty(rig.Hw.Monitor.Writes);
        Assert.Empty(rig.Hw.Modes.Switches);
        Assert.Equal(0, rig.Hw.Modes.PersistCount);

        Assert.Equal(new RecoveryReport(true, true, true, true), rig.Recovery().ResetToDefaults());
        Assert.Equal((1, 1, 1), rig.Writes());
    }

    /// <summary>Her şey zaten varsayılandaysa hiçbir yazma (ve yönetici onayı) yok.</summary>
    [Fact]
    public void ResetToDefaults_WhenAlreadyDefault_WritesNothing()
    {
        var rig = new Rig(originalGammaRange: null);

        Assert.Equal(new RecoveryReport(true, true, true, true), rig.Recovery().ResetToDefaults());
        Assert.Equal((0, 0, 0), rig.Writes());
    }

    /// <summary>
    /// İlk açılış yedeği yoksa uygulama GdiIcmGammaRange'e hiç yazmamıştır (her yazmadan önce yedek alınır): dokunulmaz.
    /// Kurtarma yeni bir ilk açılış yedeği oluşturmaz (o anki bozuk hâl "fabrika ayarı" olmasın).
    /// </summary>
    [Fact]
    public void ResetToDefaults_WithoutFactoryBackup_LeavesGammaRange_AndCreatesNoBackup()
    {
        var rig = new Rig(originalGammaRange: null, withBackup: false);
        rig.LeaveCrashedState();

        Assert.Equal(new RecoveryReport(true, true, true, true), rig.Recovery().ResetToDefaults());

        Assert.Equal(ColorPipeline.FullGammaRange, rig.Hw.GammaRange.Read());
        Assert.Equal(GammaRamp.Identity, rig.Hw.Gpu.Read(Display));
        Assert.Equal(FakeNvidiaSettings.DriverDefault, rig.Hw.Nvidia.Read(Display));
        Assert.Null(rig.Store.Load());
        Assert.Equal(0, rig.Store.SaveCount);
    }

    /// <summary>Güvenli mod: NVIDIA sürücüsü ve GPU tablosu yok. Olmayanlar false, kayıt defteri yine döner; hata atılmaz.</summary>
    [Fact]
    public void ResetToDefaults_InSafeMode_RestoresGammaRange_AndReportsTheRest()
    {
        var rig = new Rig(originalGammaRange: null);
        rig.LeaveCrashedState();
        rig.Hw.Nvidia.Available = false;
        rig.Hw.Gpu.ReadFails = true;

        Assert.Equal(new RecoveryReport(false, true, false, false), rig.Recovery().ResetToDefaults());
        Assert.Null(rig.Hw.GammaRange.Read());
        Assert.Equal((0, 0, 1), rig.Writes());
    }

    /// <summary>Bir adımın başarısızlığı diğerlerini durdurmaz: tablo reddedilir, UAC'de "Hayır" denir.</summary>
    [Fact]
    public void ResetToDefaults_WhenTableRejectedAndAdminRefuses_StillResetsNvidia()
    {
        var rig = new Rig(originalGammaRange: null);
        rig.LeaveCrashedState();
        rig.Hw.Gpu.RejectWrites = true;
        rig.Hw.GammaRange.AdminApproves = false;

        Assert.Equal(new RecoveryReport(false, false, true, true), rig.Recovery().ResetToDefaults());
        Assert.Equal(Dark, rig.Hw.Gpu.Read(Display));
        Assert.Equal(ColorPipeline.FullGammaRange, rig.Hw.GammaRange.Read());
        Assert.Equal(FakeNvidiaSettings.DriverDefault, rig.Hw.Nvidia.Read(Display));
    }

    /// <summary>Sürücü birim tabloyu yuvarlayıp Windows'un nötr tablosu (i×256) olarak saklarsa da sıfırlanmış sayılır.</summary>
    [Fact]
    public void ResetToDefaults_CountsRoundedNeutralTableAsReset()
    {
        var rig = new Rig(originalGammaRange: null);
        rig.Hw.Gpu.StoredBits = 8; // sürücü tabloyu yuvarlar: birim tablo i×256 okunur
        rig.Hw.Gpu.SimulateExternalChange(Display, Dark);

        var report = rig.Recovery().ResetToDefaults();

        Assert.True(report.GpuTableReset);
        Assert.NotEqual(GammaRamp.Identity, rig.Hw.Gpu.Read(Display));
    }

    private sealed class Rig(int? originalGammaRange, bool withBackup = true)
    {
        public FakeHardware Hw { get; } = new(76);
        public MemoryBackupStore Store { get; } = new(withBackup
            ? new FactoryBackup(new Dictionary<byte, ushort>(), GammaRamp.Identity, FakeNvidiaSettings.DriverDefault, originalGammaRange)
            : null);

        private int _gpu, _nvidia, _range;

        public IRecovery Recovery() => MonitorTuner.Services.Recovery.Create(
            Display, new SafetyHardware(Hw.Monitor, Hw.Gpu, Hw.Modes, Hw.Nvidia, Hw.GammaRange), Store, Hw.Clock);

        /// <summary>Uygulamanın çökmeden önce yazdıkları; sayaçlar bundan sonrasını sayar.</summary>
        public void LeaveCrashedState()
        {
            if (originalGammaRange is { } range) Hw.GammaRange.Write(range, Point(WriteTarget.GdiIcmGammaRange));
            Hw.Gpu.SimulateExternalChange(Display, Dark);
            Hw.Nvidia.SetDithering(Display, DitheringMode.On, Point(WriteTarget.NvidiaSettings));
            Hw.Nvidia.SetVibrance(Display, 70, Point(WriteTarget.NvidiaSettings));
            Hw.GammaRange.Write(ColorPipeline.FullGammaRange, Point(WriteTarget.GdiIcmGammaRange));
            (_gpu, _nvidia, _range) = (Hw.Gpu.Writes.Count, Hw.Nvidia.WriteCount, Hw.GammaRange.WriteCount);
        }

        /// <summary>Kurtarmanın yazmaları (çökmeden kalanlar sayılmaz): tablo, NVIDIA, kayıt defteri.</summary>
        public (int Gpu, int Nvidia, int Range) Writes() =>
            (Hw.Gpu.Writes.Count - _gpu, Hw.Nvidia.WriteCount - _nvidia, Hw.GammaRange.WriteCount - _range);

        private static RestorePoint Point(WriteTarget target) => FakeRestorePoints.For(target, Display);
    }

    private sealed class MemoryBackupStore(FactoryBackup? saved) : IFactoryBackupStore
    {
        public int SaveCount { get; private set; }

        public FactoryBackup? Load() => saved;

        public void Save(FactoryBackup backup)
        {
            saved = backup;
            SaveCount++;
        }
    }
}
