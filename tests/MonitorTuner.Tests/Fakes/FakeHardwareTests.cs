using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;

namespace MonitorTuner.Tests.Fakes;

/// <summary>Sahte donanımın kendisi (T003): sonraki görevlerin testleri bunlara dayanır.</summary>
public class FakeHardwareTests
{
    private static readonly DisplayId Display = FakeHardware.Display;

    private static RestorePoint Ddc(byte code) => FakeRestorePoints.For(WriteTarget.Ddc, Display, code);

    [Fact]
    public void EveryHardwareInterface_HasAFake()
    {
        var interfaces = typeof(RestorePoint).Assembly.GetExportedTypes().Where(t => t.IsInterface);
        var fakes = typeof(FakeHardware).Assembly.GetTypes().Where(t => t.Namespace == typeof(FakeHardware).Namespace).ToList();
        Assert.All(interfaces, i => Assert.Contains(fakes, f => i.IsAssignableFrom(f)));
    }

    [Fact]
    public void SampleEdid_IsTheRecordedHpP21bG4()
    {
        var edid = SampleEdid.HpP21bG4;
        var hex = File.ReadAllText(Path.Combine(RepoRoot.Find(), "donanim", "hp-p21b-g4", "edid-original.txt"))
            .Split((char[])[' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(b => Convert.ToByte(b, 16));

        Assert.Equal(256, edid.Length);
        Assert.Equal(hex, edid);
        Assert.Equal([0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00], edid[..8]);
        Assert.All([0, 128], start => Assert.Equal(0, edid.Skip(start).Take(128).Sum(b => b) % 256));
        var text = System.Text.Encoding.ASCII.GetString(edid);
        Assert.Contains("HP P21b G4", text);
        Assert.Contains("1CR1481CN2", text);
    }

    [Fact]
    public void Enumerator_ListsHpOnHdmiOfRtx3080_WithSampleEdid()
    {
        var display = Assert.Single(new FakeHardware().Displays.EnumerateDisplays());
        Assert.Equal(Display, display.Display);
        Assert.Equal(ConnectionType.Hdmi, display.Connection);
        Assert.Contains("3080", display.AdapterName);
        Assert.Equal(SampleEdid.HpP21bG4, display.Edid);
    }

    [Fact]
    public void DdcMonitor_StartsWithRecordedValues_AndCountsMemoryWrites()
    {
        var monitor = FakeDdcMonitor.HpP21bG4();

        Assert.Equal(new DdcValue(55, 100), monitor.Read(Display, 0x10));
        Assert.Equal(new DdcValue(6000, 0), monitor.Read(Display, 0xAE));
        Assert.Null(monitor.Read(Display, 0x04)); // sadece yazılır
        Assert.Null(monitor.Read(Display, 0x99)); // bilinmeyen kod
        Assert.Contains("vcp(", monitor.ReadCapabilities(Display));
        Assert.Contains(" 10 ", monitor.ReadCapabilities(Display));

        Assert.True(monitor.Write(Display, 0x10, 40, Ddc(0x10)));
        Assert.True(monitor.Write(Display, 0x10, 41, Ddc(0x10)));
        Assert.False(monitor.Write(Display, 0xAE, 7600, Ddc(0xAE))); // sadece okunur
        Assert.False(monitor.Write(Display, 0x10, 101, Ddc(0x10))); // en büyük değerin üstü

        Assert.Equal(new DdcValue(41, 100), monitor.Read(Display, 0x10));
        Assert.Equal(2, monitor.WriteCount(0x10));
        Assert.Equal(0, monitor.WriteCount(0xAE));
        Assert.Equal(2, monitor.Writes.Count);
    }

    [Fact]
    public void DdcMonitor_AcceptsForbiddenCodes_SoTestsCanCatchThemReachingIt()
    {
        var monitor = FakeDdcMonitor.HpP21bG4();
        Assert.True(monitor.Write(Display, 0x04, 1, Ddc(0x04)));
        Assert.Equal(1, monitor.WriteCount(0x04));
    }

    [Fact]
    public void DdcMonitor_WithoutDdc_ReadsNothing_AndWritesNothing()
    {
        var monitor = FakeDdcMonitor.WithoutDdc();
        Assert.Null(monitor.ReadCapabilities(Display));
        Assert.Null(monitor.Read(Display, 0x10));
        Assert.False(monitor.Write(Display, 0x10, 40, Ddc(0x10)));
        Assert.Empty(monitor.Writes);
    }

    [Fact]
    public void DdcMonitor_NotResponding_FailsReadsAndWrites()
    {
        var monitor = FakeDdcMonitor.HpP21bG4();
        monitor.Responding = false;
        Assert.Null(monitor.Read(Display, 0x10));
        Assert.False(monitor.Write(Display, 0x10, 40, Ddc(0x10)));
        Assert.Equal(0, monitor.WriteCount(0x10));
    }

    [Fact]
    public void EveryFake_RejectsRestorePointForAnotherTarget()
    {
        var hw = new FakeHardware();
        var gpuPoint = FakeRestorePoints.For(WriteTarget.GpuGammaTable, Display);

        Assert.Throws<InvalidOperationException>(() => hw.Monitor.Write(Display, 0x12, 50, Ddc(0x10)));
        Assert.Throws<InvalidOperationException>(() => hw.Monitor.Write(Display, 0x10, 50, gpuPoint));
        Assert.Throws<InvalidOperationException>(() => hw.Modes.SwitchMode(Display, new DisplayMode(1920, 1080, 60), gpuPoint));
        Assert.Throws<InvalidOperationException>(() => hw.Nvidia.SetVibrance(Display, 60, gpuPoint));
        Assert.Throws<InvalidOperationException>(() => hw.GammaRange.Write(256, gpuPoint));
        var other = FakeRestorePoints.For(WriteTarget.GpuGammaTable, new DisplayId(@"\\.\DISPLAY2"));
        Assert.Throws<InvalidOperationException>(() => hw.Gpu.Write(Display, GammaRamp.Identity, other));
        Assert.Empty(hw.Monitor.Writes);
    }

    [Fact]
    public void GpuTable_StartsIdentity_RecordsOwnWrites_AndSeesExternalChanges()
    {
        var gpu = new FakeGpuGammaTable(Display);
        var point = FakeRestorePoints.For(WriteTarget.GpuGammaTable, Display);
        var dark = Ramp(v => (ushort)(v / 2));

        Assert.Equal(GammaRamp.Identity, gpu.Read(Display));
        Assert.True(gpu.Write(Display, dark, point));
        Assert.Equal(dark, gpu.Read(Display));

        gpu.SimulateExternalChange(Display, GammaRamp.Identity);
        Assert.Equal(GammaRamp.Identity, gpu.Read(Display));
        Assert.Single(gpu.Writes);

        gpu.RejectWrites = true;
        Assert.False(gpu.Write(Display, dark, point));
        Assert.Equal(GammaRamp.Identity, gpu.Read(Display));
        gpu.ReadFails = true;
        Assert.Null(gpu.Read(Display));
    }

    [Fact]
    public void Nvidia_SetsAndResetsDitheringAndVibrance()
    {
        var nvidia = new FakeNvidiaSettings(Display);
        var point = FakeRestorePoints.For(WriteTarget.NvidiaSettings, Display);

        Assert.Equal(FakeNvidiaSettings.DriverDefault, nvidia.Read(Display));
        Assert.True(nvidia.SetDithering(Display, DitheringMode.On, point));
        Assert.True(nvidia.SetVibrance(Display, 65, point));
        Assert.False(nvidia.SetVibrance(Display, 101, point));
        Assert.Equal(new NvidiaState(DitheringMode.On, 65), nvidia.Read(Display));

        Assert.True(nvidia.ResetToDefaults(Display, point));
        Assert.Equal(FakeNvidiaSettings.DriverDefault, nvidia.Read(Display));
        Assert.Equal(3, nvidia.WriteCount);

        nvidia.Available = false;
        Assert.Null(nvidia.Read(Display));
        Assert.False(nvidia.SetVibrance(Display, 60, point));
    }

    [Fact]
    public void DisplayModes_SwitchOnlyBetweenListedModes_AndMonitorReportsNewHz()
    {
        var hw = new FakeHardware(60, 72, 76);
        var point = FakeRestorePoints.For(WriteTarget.DisplayMode, Display);
        var before = hw.Modes.ListModes(Display).ToList();

        Assert.Equal(new DisplayMode(1920, 1080, 60), hw.Modes.GetCurrentMode(Display));
        Assert.False(hw.Modes.SwitchMode(Display, new DisplayMode(1920, 1080, 75), point));
        Assert.Equal(before, hw.Modes.ListModes(Display));

        Assert.True(hw.Modes.SwitchMode(Display, new DisplayMode(1920, 1080, 76), point));
        Assert.Equal(76, hw.Modes.GetCurrentMode(Display)?.RefreshHz);
        Assert.Equal(new DdcValue(7600, 0), hw.Monitor.Read(Display, 0xAE));
        Assert.Single(hw.Modes.Switches);

        hw.Modes.SimulateDriverDroppedMode(new DisplayMode(1920, 1080, 72));
        Assert.DoesNotContain(new DisplayMode(1920, 1080, 72), hw.Modes.ListModes(Display));
        hw.Modes.RejectSwitches = true;
        Assert.False(hw.Modes.SwitchMode(Display, new DisplayMode(1920, 1080, 60), point));
    }

    [Fact]
    public void GdiIcmGammaRange_WritesOnlyWithAdminApproval_AndCanBeDeleted()
    {
        var registry = new FakeGdiIcmGammaRange();
        var point = FakeRestorePoints.For(WriteTarget.GdiIcmGammaRange, Display);

        Assert.Null(registry.Read());
        registry.AdminApproves = false;
        Assert.False(registry.Write(256, point));
        Assert.Null(registry.Read());

        registry.AdminApproves = true;
        Assert.True(registry.Write(256, point));
        Assert.Equal(256, registry.Read());
        Assert.True(registry.Write(null, point));
        Assert.Null(registry.Read());
        Assert.Equal(2, registry.WriteCount);
    }

    [Fact]
    public void Clock_FiresConfirmationTimerOnlyAfter15Seconds()
    {
        var clock = new FakeHardware().Clock;
        var fired = 0;
        using var timer = clock.CreateTimer(_ => fired++, null, SafetyLimits.ConfirmationTimeout, Timeout.InfiniteTimeSpan);

        clock.Advance(TimeSpan.FromSeconds(14.9));
        Assert.Equal(0, fired);
        clock.Advance(TimeSpan.FromSeconds(0.1));
        Assert.Equal(1, fired);
        Assert.Equal(FakeHardware.Start + SafetyLimits.ConfirmationTimeout, clock.GetUtcNow());
    }

    private static GammaRamp Ramp(Func<ushort, ushort> f)
    {
        var values = GammaRamp.Identity.Red.Select(f).ToArray();
        return new GammaRamp(values, values, values);
    }
}
