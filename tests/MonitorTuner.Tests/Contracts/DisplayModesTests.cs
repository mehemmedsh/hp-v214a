using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Tests.Fakes;

namespace MonitorTuner.Tests.Contracts;

/// <summary>Windows ekran katmanı sözleşmesi (02-MIMARI.md), sahte sistemle (T209).</summary>
public class DisplayModesTests
{
    private static readonly DisplayId Display = FakeHardware.Display;

    /// <summary>Listede olmayan moda geçiş başarısız; 60 Hz dahil liste değişmez.</summary>
    [Fact]
    public void SwitchMode_ToModeNotInList_Fails_AndListIsUnchanged()
    {
        var hw = new FakeHardware(76);
        var point = FakeRestorePoints.For(WriteTarget.DisplayMode, Display);
        var before = hw.Modes.ListModes(Display).ToList();

        Assert.False(hw.Modes.SwitchMode(Display, new DisplayMode(1920, 1080, 77), point));
        Assert.False(hw.Modes.SwitchMode(Display, new DisplayMode(2080, 1092, 76), point));

        Assert.Equal(before, hw.Modes.ListModes(Display));
        Assert.Contains(new DisplayMode(1920, 1080, 60), hw.Modes.ListModes(Display));
        Assert.Equal(60, hw.Modes.Current.RefreshHz);
    }

    /// <summary>Geçiş anlıktır; kalıcı kayıt ayrı ve sadece şu anki mod için. Yeniden başlatmada kayıtlı mod gelir.</summary>
    [Fact]
    public void SwitchMode_IsNotPersisted_UntilPersistMode()
    {
        var hw = new FakeHardware(76);
        var point = FakeRestorePoints.For(WriteTarget.DisplayMode, Display);
        var hz76 = new DisplayMode(1920, 1080, 76);

        Assert.False(hw.Modes.PersistMode(Display, hz76, point)); // şu anki mod değil
        Assert.True(hw.Modes.SwitchMode(Display, hz76, point));
        hw.Modes.SimulateRestart();
        Assert.Equal(60, hw.Modes.Current.RefreshHz);

        Assert.True(hw.Modes.SwitchMode(Display, hz76, point));
        Assert.True(hw.Modes.PersistMode(Display, hz76, point));
        hw.Modes.SimulateRestart();
        Assert.Equal(hz76, hw.Modes.Current);
    }
}
