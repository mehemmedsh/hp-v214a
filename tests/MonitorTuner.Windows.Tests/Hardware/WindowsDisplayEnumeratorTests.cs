using System.Runtime.InteropServices;
using MonitorTuner.Core;
using MonitorTuner.Hardware;
using Xunit.Abstractions;

namespace MonitorTuner.Windows.Tests.Hardware;

/// <summary>
/// Monitör tanımanın Windows yarısı (T314). Çözümleme yardımcıları ve P/Invoke yapı boyutları her yerde denenir (donanıma dokunmaz);
/// gerçek ekranların listelenmesi 🔧 testidir ve varsayılan atlanır.
/// </summary>
public class WindowsDisplayEnumeratorTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(0u, ConnectionType.Vga)]
    [InlineData(4u, ConnectionType.Dvi)]
    [InlineData(5u, ConnectionType.Hdmi)]
    [InlineData(10u, ConnectionType.DisplayPort)]
    [InlineData(11u, ConnectionType.DisplayPort)]
    [InlineData(0x80000000u, ConnectionType.Unknown)] // gömülü (dizüstü paneli)
    [InlineData(15u, ConnectionType.Unknown)]          // Miracast
    [InlineData(0xFFFFFFFFu, ConnectionType.Unknown)]  // bilinmiyor
    public void ConnectionOf_MapsOutputTechnology(uint technology, ConnectionType expected) =>
        Assert.Equal(expected, WindowsDisplayEnumerator.ConnectionOf(technology));

    [Fact]
    public void PortName_IsStable_ForEveryConnection()
    {
        Assert.Equal("HDMI", WindowsDisplayEnumerator.PortName(ConnectionType.Hdmi));
        Assert.Equal("DP", WindowsDisplayEnumerator.PortName(ConnectionType.DisplayPort));
        Assert.Equal("DVI", WindowsDisplayEnumerator.PortName(ConnectionType.Dvi));
        Assert.Equal("VGA", WindowsDisplayEnumerator.PortName(ConnectionType.Vga));
        Assert.Equal("Unknown", WindowsDisplayEnumerator.PortName(ConnectionType.Unknown));
    }

    [Fact]
    public void EnumKeyOf_TurnsAnInterfacePathIntoTheRegistryKey()
    {
        var key = WindowsDisplayEnumerator.EnumKeyOf(@"\\?\DISPLAY#HPN3430#5&2a3b4c5d&0&UID4352#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}");

        Assert.Equal(@"SYSTEM\CurrentControlSet\Enum\DISPLAY\HPN3430\5&2a3b4c5d&0&UID4352\Device Parameters", key);
    }

    [Theory]
    [InlineData("")]
    [InlineData(@"MONITOR\HPN3430\{4d36e96e-e325-11ce-bfc1-08002be10318}\0001")] // arayüz yolu değil (bayraksız çağrı)
    [InlineData(@"\\?\USB#VID_0000#1234#{guid}")]
    [InlineData(@"\\?\DISPLAY##5&1&0#{guid}")]
    [InlineData(@"\\?\DISPLAY#HPN3430")]
    public void EnumKeyOf_RejectsUnknownFormats(string path) => Assert.Null(WindowsDisplayEnumerator.EnumKeyOf(path));

    /// <summary>QueryDisplayConfig yapıları elle yazıldı; boyutları Windows SDK'dakiyle aynı olmalı, yoksa çağrı bellek bozar.</summary>
    [Fact]
    public void NativeStructs_HaveTheWindowsSdkSizes()
    {
        Assert.Equal(20, Marshal.SizeOf<WindowsDisplayEnumerator.PathSourceInfo>());
        Assert.Equal(48, Marshal.SizeOf<WindowsDisplayEnumerator.PathTargetInfo>());
        Assert.Equal(72, Marshal.SizeOf<WindowsDisplayEnumerator.PathInfo>());
        Assert.Equal(64, Marshal.SizeOf<WindowsDisplayEnumerator.ModeInfo>());
        Assert.Equal(20, Marshal.SizeOf<WindowsDisplayEnumerator.DeviceInfoHeader>());
        Assert.Equal(84, Marshal.SizeOf<WindowsDisplayEnumerator.SourceDeviceName>());
    }

    /// <summary>MONITORTUNER_REAL_DISPLAYS=1: bağlı ekranları listeler (yazmaz). HP P21b G4 HDMI'da, EDID'i çözülebilir olmalı.</summary>
    [RealMonitorFact("MONITORTUNER_REAL_DISPLAYS")]
    public void RealDisplays_AreListed_WithEdidAndConnection()
    {
        var displays = new WindowsDisplayEnumerator().EnumerateDisplays();

        Assert.NotEmpty(displays);
        foreach (var d in displays)
        {
            var edid = EdidInfo.TryParse(d.Edid);
            output.WriteLine($"{d.Display.Value} · {edid?.ModelName ?? "(EDID yok)"} · seri {edid?.SerialText} · {d.Connection} ({d.Port}) · {d.AdapterName}");
        }
        var hp = displays.FirstOrDefault(d => EdidInfo.TryParse(d.Edid)?.ModelName == "HP P21b G4");
        Assert.NotNull(hp);
        Assert.Equal(ConnectionType.Hdmi, hp.Connection);
    }
}
