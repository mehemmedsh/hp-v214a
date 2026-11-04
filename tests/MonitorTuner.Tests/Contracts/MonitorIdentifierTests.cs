using MonitorTuner.Core;
using MonitorTuner.Core.Profiles;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Services;
using MonitorTuner.Tests.Fakes;

namespace MonitorTuner.Tests.Contracts;

/// <summary>Monitör tanıma sözleşmesi (02-MIMARI.md), sahte sistemle (T102).</summary>
public class MonitorIdentifierTests
{
    private static MonitorProfile HpProfile() =>
        new MonitorProfileReader().Read(File.ReadAllText(Path.Combine(RepoRoot.Find(), "profiles", "hp-p21b-g4.json")));

    /// <summary>Örnek EDID'den HP P21b G4, seri no, HDMI ve kart adı okunur.</summary>
    [Fact]
    public void Identify_ReadsModelSerialConnectionAndAdapter_FromSampleEdid()
    {
        var hardware = new FakeHardware();

        var monitor = Assert.Single(new MonitorIdentifier(hardware.Displays).Identify());

        Assert.Equal(FakeHardware.Display, monitor.Display);
        Assert.Equal("HP P21b G4", monitor.Model);
        Assert.Equal("1CR1481CN2", monitor.SerialNumber);
        Assert.Equal(ConnectionType.Hdmi, monitor.Connection);
        Assert.Equal("HDMI", monitor.Port);
        Assert.Equal("NVIDIA GeForce RTX 3080", monitor.AdapterName);
        Assert.Equal(new MonitorKey("HP P21b G4", "1CR1481CN2", "HDMI", "NVIDIA GeForce RTX 3080"), monitor.Key);
    }

    /// <summary>Tanınan monitör profildekiyle eşleşir: HP P21b G4 doğrulanır, başka model eşleşmez.</summary>
    [Fact]
    public void Identify_MatchesProfile_OnlyForHpP21bG4()
    {
        var hardware = new FakeHardware();
        var other = SampleEdid.HpP21bG4;
        other[90 + 5 + 4] = (byte)'Q'; // model adı "HP P21b G4" → "HP PQ1b G4"
        hardware.Displays.Connected.Add(new ConnectedDisplay(new DisplayId(@"\\.\DISPLAY2"), other, ConnectionType.DisplayPort, "DP", "NVIDIA GeForce RTX 3090"));

        var monitors = new MonitorIdentifier(hardware.Displays).Identify();

        Assert.Equal(2, monitors.Count);
        Assert.True(monitors[0].Matches(HpProfile()));
        Assert.Equal("HP PQ1b G4", monitors[1].Model);
        Assert.False(monitors[1].Matches(HpProfile()));
    }

    /// <summary>VGA bağlantısı tanınır (arayüz "HDMI'a geç" uyarısı verir).</summary>
    [Fact]
    public void Identify_MarksVgaConnection_ForWarning()
    {
        var hardware = new FakeHardware();
        hardware.Displays.Connected[0] = hardware.Displays.Connected[0] with { Connection = ConnectionType.Vga, Port = "VGA" };

        var monitor = Assert.Single(new MonitorIdentifier(hardware.Displays).Identify());

        Assert.Equal(ConnectionType.Vga, monitor.Connection);
        Assert.True(monitor.IsVga);
        Assert.False(new MonitorIdentifier(new FakeHardware().Displays).Identify()[0].IsVga);
    }

    /// <summary>Bozuk EDID listeden düşmez ve hata fırlatmaz; model ve seri boş kalır, profille eşleşmez (yazma yapılmaz).</summary>
    [Fact]
    public void Identify_UnreadableEdid_StillListsDisplayWithoutModel()
    {
        var hardware = new FakeHardware();
        hardware.Displays.Connected[0] = hardware.Displays.Connected[0] with { Edid = [1, 2, 3] };

        var monitor = Assert.Single(new MonitorIdentifier(hardware.Displays).Identify());

        Assert.Equal("", monitor.Model);
        Assert.Equal("", monitor.SerialNumber);
        Assert.Equal("HDMI", monitor.Port);
        Assert.False(monitor.Matches(HpProfile()));
    }

    [Fact]
    public void Identify_NoDisplays_ReturnsEmptyList() =>
        Assert.Empty(new MonitorIdentifier(new FakeDisplayEnumerator()).Identify());
}
