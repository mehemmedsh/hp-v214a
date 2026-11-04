using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;
using MonitorTuner.Tests.Fakes;

namespace MonitorTuner.Tests.Contracts;

/// <summary>Güvenlik katmanı sözleşmesi (02-MIMARI.md, 03-GUVENLIK.md): sahte donanım ve sahte saatle (T103).</summary>
public class SafetyLayerTests
{
    private const byte Brightness = 0x10, Contrast = 0x12, RedGain = 0x16;
    private static readonly DisplayId Display = FakeHardware.Display;
    private static readonly GammaRamp Dim = Scaled(0.5), Warm = Scaled(0.9);

    /// <summary>Geri alma noktası yazmadan önce mevcut değeri yedekler.</summary>
    [Fact]
    public void CreateRestorePoint_BacksUpCurrentValue_BeforeWrite()
    {
        var rig = new Rig();
        var point = rig.Safety.CreateRestorePoint(Ddc(Brightness));
        Assert.Equal(Ddc(Brightness), point.Request);
        Assert.Equal(FakeHardware.Start, point.CreatedAt);

        Assert.True(rig.Hw.Monitor.Write(Display, Brightness, 30, point));
        rig.Safety.RollBack(point);

        Assert.Equal(55, rig.Hw.Monitor.Read(Display, Brightness)!.Value.Current);
        Assert.Equal<ushort>([30, 55], rig.Hw.Monitor.Writes.Select(w => w.Value));
        Assert.Equal([point], rig.RolledBack);
    }

    public static TheoryData<WriteTarget> Targets => new(Enum.GetValues<WriteTarget>());

    /// <summary>Bitti sayılır: sahte donanımla her yazma geri alınabiliyor.</summary>
    [Theory, MemberData(nameof(Targets))]
    public void EveryWriteTarget_CanBeRolledBack(WriteTarget target)
    {
        var rig = new Rig();
        var before = rig.Snapshot();
        var point = rig.Safety.CreateRestorePoint(new WriteRequest(target, Display, target == WriteTarget.Ddc ? Contrast : null));

        Assert.True(rig.Change(point));
        Assert.NotEqual(before, rig.Snapshot());
        rig.Safety.RollBack(point);

        Assert.Equal(before, rig.Snapshot());
    }

    [Fact]
    public void CreateRestorePoint_Refuses_UnsafeRequests_AndValuesItCannotBackUp()
    {
        var rig = new Rig();
        Assert.Throws<ArgumentException>(() => rig.Safety.CreateRestorePoint(Ddc(0x04))); // fabrika sıfırlaması
        Assert.Throws<ArgumentException>(() => rig.Safety.CreateRestorePoint(Ddc(0xE6))); // üreticiye özel
        Assert.Throws<ArgumentException>(() => rig.Safety.CreateRestorePoint(new WriteRequest(WriteTarget.Ddc, Display)));
        Assert.Throws<ArgumentException>(() => rig.Safety.CreateRestorePoint(new WriteRequest(WriteTarget.GpuGammaTable, Display, Brightness)));
        Assert.Throws<ArgumentException>(() => rig.Safety.CreateRestorePoint(new WriteRequest(WriteTarget.GpuGammaTable, new DisplayId(@"\\.\DISPLAY2"))));

        rig.Hw.Monitor.Responding = false;
        rig.Hw.Gpu.ReadFails = true;
        rig.Hw.Nvidia.Available = false;
        Assert.Throws<InvalidOperationException>(() => rig.Safety.CreateRestorePoint(Ddc(Brightness)));
        Assert.Throws<InvalidOperationException>(() => rig.Safety.CreateRestorePoint(Gpu()));
        Assert.Throws<InvalidOperationException>(() => rig.Safety.CreateRestorePoint(new WriteRequest(WriteTarget.NvidiaSettings, Display)));
    }

    /// <summary>Onaylanmayan değişiklik sahte saatte 15 sn sonra geri alınır; onaylanan kalır.</summary>
    [Fact]
    public void UnconfirmedChange_RollsBackAfter15Seconds()
    {
        var rig = new Rig();
        var point = rig.SwitchTo(76);
        rig.Safety.RequireConfirmation(point);

        rig.Hw.Clock.Advance(SafetyLimits.ConfirmationTimeout - TimeSpan.FromMilliseconds(1));
        Assert.Equal(76, rig.Hw.Modes.Current.RefreshHz);
        Assert.Empty(rig.RolledBack);

        rig.Hw.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(60, rig.Hw.Modes.Current.RefreshHz);
        Assert.Equal([point], rig.RolledBack);

        var confirmed = rig.SwitchTo(76);
        rig.Safety.RequireConfirmation(confirmed);
        rig.Hw.Clock.Advance(TimeSpan.FromSeconds(5));
        rig.Safety.Confirm(confirmed);
        rig.Hw.Clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(76, rig.Hw.Modes.Current.RefreshHz);
        Assert.Equal([point], rig.RolledBack);
    }

    [Fact]
    public void ManualRollBack_WhileWaiting_StopsTimer_AndLateConfirmIsHarmless()
    {
        var rig = new Rig();
        var point = rig.SwitchTo(76);
        rig.Safety.RequireConfirmation(point);

        rig.Safety.RollBack(point);
        rig.Hw.Clock.Advance(TimeSpan.FromMinutes(1));
        rig.Safety.Confirm(point);

        Assert.Equal([76, 60], rig.Hw.Modes.Switches.Select(s => s.Mode.RefreshHz));
        Assert.Equal([point], rig.RolledBack);
        Assert.Throws<ArgumentException>(() => rig.Safety.RollBack(point)); // zaten geri alındı
    }

    [Fact]
    public void FailedRollBack_IsReported_AndCanBeRetried()
    {
        var rig = new Rig();
        var point = rig.SwitchTo(76);
        rig.Safety.RequireConfirmation(point);
        rig.Hw.Modes.RejectSwitches = true;

        rig.Hw.Clock.Advance(SafetyLimits.ConfirmationTimeout);
        Assert.Equal([point], rig.Failed);
        Assert.Empty(rig.RolledBack);
        Assert.Throws<InvalidOperationException>(() => rig.Safety.RollBack(point));

        rig.Hw.Modes.RejectSwitches = false;
        rig.Safety.RollBack(point);
        Assert.Equal(60, rig.Hw.Modes.Current.RefreshHz);
        Assert.Equal([point], rig.RolledBack);
    }

    /// <summary>GPU tablosunu geri yüklemeyi Güvenlik kendisi yazmaz, IGpuTableOwner'dan ister.</summary>
    [Fact]
    public void RollBack_OfGpuTable_AsksColorPipelineToRestore()
    {
        var rig = new Rig();
        var point = rig.Safety.CreateRestorePoint(Gpu());
        Assert.True(rig.Hw.Gpu.Write(Display, Dim, point));

        rig.Safety.RollBack(point);
        Assert.Equal([(Display, GammaRamp.Identity)], rig.Owner.Restores);
        Assert.Equal(GammaRamp.Identity, rig.Hw.Gpu.Read(Display));

        // Boru hattı geri yüklemezse geri alındı sayılmaz.
        var second = rig.Safety.CreateRestorePoint(Gpu());
        Assert.True(rig.Hw.Gpu.Write(Display, Dim, second));
        rig.Owner.Ignore = true;
        Assert.Throws<InvalidOperationException>(() => rig.Safety.RollBack(second));
    }

    /// <summary>Fabrika ayarı ve kaldırma temizliği yedekteki değerlere döner.</summary>
    [Fact]
    public void RestoreFactoryDefaults_And_Uninstall_ReturnEverythingToBackup()
    {
        var rig = new Rig();
        rig.Hw.Gpu.SimulateExternalChange(Display, Warm); // kullanıcının ilk açılıştaki tablosu

        // İlk yazmadan önce ilk açılış yedeği alınır ve saklanır.
        var first = rig.Safety.CreateRestorePoint(Ddc(Brightness));
        var saved = Assert.Single(rig.Store.Saves);
        Assert.Equal<ushort>(55, saved.Ddc[Brightness]);
        Assert.Equal<ushort>(72, saved.Ddc[Contrast]);
        Assert.Equal(Warm, saved.GpuTable);
        Assert.Equal(FakeNvidiaSettings.DriverDefault, saved.Nvidia);
        Assert.Null(saved.GammaRange);

        Assert.True(rig.Hw.Monitor.Write(Display, Brightness, 30, first));
        foreach (var target in new[] { WriteTarget.GpuGammaTable, WriteTarget.NvidiaSettings, WriteTarget.GdiIcmGammaRange })
            Assert.True(rig.Change(rig.Safety.CreateRestorePoint(new WriteRequest(target, Display))));
        rig.SwitchTo(76);
        var ddcWrites = rig.Hw.Monitor.Writes.Count;

        rig.Safety.RestoreFactoryDefaults();
        Assert.Equal(55, rig.Hw.Monitor.Read(Display, Brightness)!.Value.Current);
        Assert.Equal(ddcWrites + 1, rig.Hw.Monitor.Writes.Count); // sadece değişen kod yazıldı (monitör belleği)
        Assert.Equal(Warm, rig.Hw.Gpu.Read(Display));
        Assert.Equal(Warm, rig.Owner.Restores[^1].Table);
        Assert.Equal(FakeNvidiaSettings.DriverDefault, rig.Hw.Nvidia.Read(Display));
        Assert.Equal(256, rig.Hw.GammaRange.Read()); // uygulamanın ihtiyacı; sadece kaldırmada döner
        Assert.Equal(76, rig.Hw.Modes.Current.RefreshHz); // Hz'e dokunmaz

        rig.Safety.CleanUpForUninstall();
        Assert.Equal(GammaRamp.Identity, rig.Hw.Gpu.Read(Display));
        Assert.Null(rig.Hw.GammaRange.Read());
        Assert.Equal(55, rig.Hw.Monitor.Read(Display, Brightness)!.Value.Current);
        Assert.Equal(76, rig.Hw.Modes.Current.RefreshHz);
        Assert.Single(rig.Store.Saves); // yedek yeniden alınmadı
    }

    [Fact]
    public void FactoryBackup_FromStore_IsUsed_AndMissingValuesAreAddedBeforeFirstWrite()
    {
        var stored = new FactoryBackup(
            new Dictionary<byte, ushort> { [Brightness] = 80 }, GpuTable: null, FakeNvidiaSettings.DriverDefault with { Vibrance = 60 }, GammaRange: null);
        var rig = new Rig(stored);

        var point = rig.Safety.CreateRestorePoint(Ddc(Contrast));
        Assert.True(rig.Hw.Monitor.Write(Display, Contrast, 40, point));
        rig.Safety.CreateRestorePoint(Gpu());

        var saved = rig.Store.Saves[^1];
        Assert.Equal<ushort>(80, saved.Ddc[Brightness]);
        Assert.Equal<ushort>(72, saved.Ddc[Contrast]);
        Assert.Equal(GammaRamp.Identity, saved.GpuTable);

        rig.Safety.RestoreFactoryDefaults();
        Assert.Equal(80, rig.Hw.Monitor.Read(Display, Brightness)!.Value.Current);
        Assert.Equal(72, rig.Hw.Monitor.Read(Display, Contrast)!.Value.Current);
        Assert.Equal(60, rig.Hw.Nvidia.Read(Display)!.Value.Vibrance);
    }

    [Fact]
    public void RestoreFactoryDefaults_ContinuesPastFailures_ThenReportsThem()
    {
        var rig = new Rig();
        var point = rig.Safety.CreateRestorePoint(Ddc(Brightness));
        Assert.True(rig.Hw.Monitor.Write(Display, Brightness, 30, point));
        rig.Hw.Nvidia.Available = false;

        var error = Assert.Throws<InvalidOperationException>(rig.Safety.RestoreFactoryDefaults);
        Assert.Contains("NVIDIA", error.Message);
        Assert.Equal(55, rig.Hw.Monitor.Read(Display, Brightness)!.Value.Current);
    }

    [Fact]
    public void OldRestorePoints_AreDropped_ButWaitingOnesAreKept()
    {
        var rig = new Rig();
        var waiting = rig.SwitchTo(76);
        rig.Safety.RequireConfirmation(waiting);
        var oldest = rig.Safety.CreateRestorePoint(Ddc(Brightness));
        for (var i = 0; i < SafetyLayer.MaxKept; i++) rig.Safety.CreateRestorePoint(Ddc(Brightness));

        Assert.Throws<ArgumentException>(() => rig.Safety.RollBack(oldest));
        rig.Hw.Clock.Advance(SafetyLimits.ConfirmationTimeout);
        Assert.Equal([waiting], rig.RolledBack);
        Assert.Equal(60, rig.Hw.Modes.Current.RefreshHz);
    }

    private static WriteRequest Ddc(byte code) => new(WriteTarget.Ddc, Display, code);

    private static WriteRequest Gpu() => new(WriteTarget.GpuGammaTable, Display);

    private static GammaRamp Scaled(double k)
    {
        var channel = Enumerable.Range(0, GammaRamp.Size).Select(i => (ushort)(i * 257 * k)).ToArray();
        return new GammaRamp(channel, channel, channel);
    }

    private sealed record State(ushort? Contrast, GammaRamp? Table, DisplayMode Mode, NvidiaState? Nvidia, int? GammaRange);

    /// <summary>Sahte sistem + güvenlik katmanı + GPU tablosunun sahibi (boru hattı gibi kendi geri alma noktasıyla yazar).</summary>
    private sealed class Rig
    {
        public Rig(FactoryBackup? stored = null)
        {
            Store = new MemoryBackupStore(stored);
            Owner = new FakeTableOwner(Hw.Gpu);
            Safety = new SafetyLayer(
                Display, [Brightness, Contrast, RedGain],
                new SafetyHardware(Hw.Monitor, Hw.Gpu, Hw.Modes, Hw.Nvidia, Hw.GammaRange),
                () => Owner, Store, Hw.Clock);
            Owner.Safety = Safety;
            Safety.RolledBack += RolledBack.Add;
            Safety.RollBackFailed += Failed.Add;
        }

        public FakeHardware Hw { get; } = new(76);
        public MemoryBackupStore Store { get; }
        public FakeTableOwner Owner { get; }
        public SafetyLayer Safety { get; }
        public List<RestorePoint> RolledBack { get; } = [];
        public List<RestorePoint> Failed { get; } = [];

        public State Snapshot() => new(
            Hw.Monitor.Read(Display, Contrast)?.Current, Hw.Gpu.Read(Display), Hw.Modes.Current, Hw.Nvidia.Read(Display), Hw.GammaRange.Read());

        /// <summary>Hedefe şu ankinden farklı bir değer yazar.</summary>
        public bool Change(RestorePoint point) => point.Request.Target switch
        {
            WriteTarget.Ddc => Hw.Monitor.Write(Display, Contrast, 40, point),
            WriteTarget.GpuGammaTable => Hw.Gpu.Write(Display, Dim, point),
            WriteTarget.DisplayMode => Hw.Modes.SwitchMode(Display, new DisplayMode(1920, 1080, 76), point),
            WriteTarget.NvidiaSettings => Hw.Nvidia.SetVibrance(Display, 80, point) && Hw.Nvidia.SetDithering(Display, DitheringMode.On, point),
            WriteTarget.GdiIcmGammaRange => Hw.GammaRange.Write(256, point),
            _ => throw new ArgumentOutOfRangeException(nameof(point)),
        };

        public RestorePoint SwitchTo(int hz)
        {
            var point = Safety.CreateRestorePoint(new WriteRequest(WriteTarget.DisplayMode, Display));
            Assert.True(Hw.Modes.SwitchMode(Display, new DisplayMode(1920, 1080, hz), point));
            return point;
        }
    }

    private sealed class FakeTableOwner(FakeGpuGammaTable gpu) : IGpuTableOwner
    {
        public ISafetyLayer? Safety { get; set; }
        public bool Ignore { get; set; }
        public List<(DisplayId Display, GammaRamp Table)> Restores { get; } = [];

        public void Restore(DisplayId display, GammaRamp table)
        {
            Restores.Add((display, table));
            if (!Ignore) gpu.Write(display, table, Safety!.CreateRestorePoint(new WriteRequest(WriteTarget.GpuGammaTable, display)));
        }
    }

    private sealed class MemoryBackupStore(FactoryBackup? stored) : IFactoryBackupStore
    {
        public List<FactoryBackup> Saves { get; } = [];
        public FactoryBackup? Load() => Saves.Count > 0 ? Saves[^1] : stored;
        public void Save(FactoryBackup backup) => Saves.Add(backup);
    }
}
