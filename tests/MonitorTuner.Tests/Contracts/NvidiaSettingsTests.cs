using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;
using MonitorTuner.Services;
using MonitorTuner.Tests.Fakes;

namespace MonitorTuner.Tests.Contracts;

/// <summary>
/// NVIDIA katmanı sözleşmesi (02-MIMARI.md, T303): dithering GPU tablosu kullanılırken açık, canlılık sadece istenince;
/// kapanışta ikisi de kullanıcının değerine (ilk açılış yedeği, yoksa sürücü varsayılanı) döner. Sahte sürücü,
/// gerçek Güvenlik katmanı ve gerçek renk boru hattıyla.
/// </summary>
public class NvidiaSettingsTests
{
    private static readonly DisplayId Display = FakeHardware.Display;
    private static readonly GammaRamp Warm = GammaRamps.Scale(new RgbGains(1, 0.8, 0.55));
    private static readonly NvidiaState Default = FakeNvidiaSettings.DriverDefault;

    /// <summary>Kapanış: dithering ve canlılık uygulama öncesindeki değere (burada sürücü varsayılanı) döner.</summary>
    [Fact]
    public void ResetToDefaults_RestoresDitheringAndVibrance()
    {
        var rig = new Rig();
        rig.Pipeline.SetLayer(ColorLayer.BlueLight, Warm);
        Assert.True(rig.Pipeline.Apply());
        Assert.True(rig.Control.SetVibrance(80));
        Assert.Equal(new NvidiaState(DitheringMode.On, 80), rig.Hw.Nvidia.Read(Display));

        rig.Pipeline.Shutdown();
        rig.Control.Shutdown();

        Assert.Equal(Default, rig.Hw.Nvidia.Read(Display));
        var writes = rig.Hw.Nvidia.WriteCount;
        rig.Control.Shutdown(); // ikinci kapanış bir şey yazmaz
        Assert.Equal(writes, rig.Hw.Nvidia.WriteCount);
    }

    /// <summary>Tablo birim değilken (efekt ya da kalibrasyon) dithering açık; tablo nötre dönünce kullanıcının değeri.</summary>
    [Fact]
    public void Dithering_OnWhileTableInUse_BackWhenNeutral()
    {
        var rig = new Rig();
        rig.Pipeline.SetLayer(ColorLayer.BlueLight, Warm);
        Assert.True(rig.Pipeline.Apply());
        Assert.Equal(DitheringMode.On, rig.Hw.Nvidia.Read(Display)!.Value.Dithering);
        Assert.Equal(Default.Vibrance, rig.Hw.Nvidia.Read(Display)!.Value.Vibrance); // canlılığa dokunmaz
        Assert.Equal(1, rig.Hw.Nvidia.WriteCount);

        Assert.True(rig.Pipeline.Apply()); // aynı tablo: yeni yazma yok
        Assert.Equal(1, rig.Hw.Nvidia.WriteCount);

        rig.Pipeline.SetLayer(ColorLayer.BlueLight, null);
        Assert.True(rig.Pipeline.Apply());
        Assert.Equal(Default, rig.Hw.Nvidia.Read(Display));
    }

    /// <summary>Nötr tablo (birim ya da Windows'un i×256 tablosu) 8 bitte seviye kaybettirmez: dithering'e dokunulmaz.</summary>
    [Fact]
    public void Dithering_UntouchedWhileTableNeutral()
    {
        var rig = new Rig();
        ushort[] windows = [.. Enumerable.Range(0, GammaRamp.Size).Select(i => (ushort)(i * 256))];
        rig.Pipeline.SetLayer(ColorLayer.Calibration, new GammaRamp(windows, windows, windows));
        Assert.True(rig.Pipeline.Apply());
        rig.Pipeline.ResetTable();

        Assert.Equal(0, rig.Hw.Nvidia.WriteCount);
    }

    /// <summary>Canlılık istenince yazılır; sıfırlama (panik, Film/Oyun dışı profil) kullanıcının değerine döner, dithering kalır.</summary>
    [Fact]
    public void Vibrance_SetAndReset_ReturnsToUsersValue_DitheringStays()
    {
        var rig = new Rig();
        Assert.True(rig.Control.ResetVibrance()); // hiç açılmadı: yazma yok
        Assert.Equal(0, rig.Hw.Nvidia.WriteCount);

        rig.Pipeline.SetLayer(ColorLayer.Calibration, Warm);
        Assert.True(rig.Pipeline.Apply());
        Assert.True(rig.Control.SetVibrance(75));
        Assert.Equal(75, rig.Control.Vibrance);
        Assert.True(rig.Control.ResetVibrance());

        Assert.Null(rig.Control.Vibrance);
        Assert.Equal(new NvidiaState(DitheringMode.On, Default.Vibrance), rig.Hw.Nvidia.Read(Display));
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.Control.SetVibrance(101));
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.Control.SetVibrance(-1));
    }

    /// <summary>Sihirbaz sırasında canlılık kapalı; askıdayken gelen istek saklanır, devam edince yazılır.</summary>
    [Fact]
    public void Vibrance_SuspendedForWizard_ResumedAfter()
    {
        var rig = new Rig();
        Assert.True(rig.Control.SetVibrance(80));

        rig.Control.Suspend();
        Assert.Equal(Default.Vibrance, rig.Hw.Nvidia.Read(Display)!.Value.Vibrance);
        var writes = rig.Hw.Nvidia.WriteCount;
        Assert.True(rig.Control.SetVibrance(70));
        Assert.Equal(writes, rig.Hw.Nvidia.WriteCount);

        rig.Control.Resume();
        Assert.Equal(70, rig.Hw.Nvidia.Read(Display)!.Value.Vibrance);
    }

    /// <summary>Çökmeden kalan değerler kullanıcının değeri sayılmaz: dönüş ilk açılış yedeğine.</summary>
    [Fact]
    public void AfterCrash_ReturnsToFactoryBackup_NotLeftover()
    {
        var rig = new Rig(new FactoryBackup(new Dictionary<byte, ushort>(), null, Default, null));
        var point = FakeRestorePoints.For(WriteTarget.NvidiaSettings, Display);
        rig.Hw.Nvidia.SetDithering(Display, DitheringMode.On, point);
        rig.Hw.Nvidia.SetVibrance(Display, 75, point);

        Assert.True(rig.Control.SetVibrance(80));
        rig.Control.TableInUse(true);
        rig.Control.Shutdown();

        Assert.Equal(Default, rig.Hw.Nvidia.Read(Display));
    }

    /// <summary>Ekran NVIDIA'da değilse ya da sürücü cevap vermezse yazma yok, hata yok; boru hattı çalışmaya devam eder.</summary>
    [Fact]
    public void DriverUnavailable_NoWritesNoThrow()
    {
        var rig = new Rig();
        rig.Hw.Nvidia.Available = false;

        Assert.False(rig.Control.SetVibrance(80));
        rig.Pipeline.SetLayer(ColorLayer.BlueLight, Warm);
        Assert.True(rig.Pipeline.Apply());
        rig.Control.Shutdown();

        Assert.Equal(0, rig.Hw.Nvidia.WriteCount);
        Assert.Equal(Combiner.Combine([Warm]), rig.Hw.Gpu.Read(Display));
    }

    private static readonly GammaTableCombiner Combiner = new();

    /// <summary>Sahte sistem + gerçek Güvenlik + boru hattı + NVIDIA denetimi (boru hattı dithering'e haber verir).</summary>
    private sealed class Rig
    {
        public Rig(FactoryBackup? saved = null)
        {
            var store = new MemoryBackupStore(saved);
            Safety = new SafetyLayer(
                Display, [], new SafetyHardware(Hw.Monitor, Hw.Gpu, Hw.Modes, Hw.Nvidia, Hw.GammaRange),
                () => Pipeline!, store, Hw.Clock);
            Control = new NvidiaControl(Display, Hw.Nvidia, Safety, store);
            Pipeline = new ColorPipeline(Display, Hw.Gpu, Hw.GammaRange, Combiner, Safety, Control);
        }

        public FakeHardware Hw { get; } = new();
        public SafetyLayer Safety { get; }
        public NvidiaControl Control { get; }
        public ColorPipeline Pipeline { get; }
    }

    private sealed class MemoryBackupStore(FactoryBackup? saved) : IFactoryBackupStore
    {
        public FactoryBackup? Load() => saved;
        public void Save(FactoryBackup backup) => saved = backup;
    }
}
