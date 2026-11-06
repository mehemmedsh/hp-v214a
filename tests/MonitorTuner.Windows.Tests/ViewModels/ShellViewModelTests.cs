using MonitorTuner.App.ViewModels;
using MonitorTuner.Core;
using MonitorTuner.Services;

namespace MonitorTuner.Windows.Tests.ViewModels;

/// <summary>Kenar çubuğundaki bilgi (T322): monitör, bağlantı, Hz (sırada okunur) ve sürüm; okunamayan Hz yazılmaz.</summary>
public class ShellViewModelTests
{
    [Fact]
    public void BeforeRefresh_ShowsMonitorAndConnection_WithoutHz()
    {
        var vm = new ShellViewModel("HP P21b G4", ConnectionType.Hdmi, () => 76, "1.2.3");

        Assert.Equal("HP P21b G4 · HDMI", vm.MonitorLine);
        Assert.Equal("Sürüm 1.2.3", vm.Version);
    }

    [Fact]
    public void Refresh_AddsTheCurrentRefreshRate()
    {
        var hz = 60;
        var vm = new ShellViewModel("HP P21b G4", ConnectionType.Hdmi, () => hz, "1.0.0");

        vm.Refresh();
        Assert.Equal("HP P21b G4 · HDMI · 60 Hz", vm.MonitorLine);

        hz = 72; // kullanıcı Windows'tan 72 Hz'e geçti
        vm.Refresh();
        Assert.Equal("HP P21b G4 · HDMI · 72 Hz", vm.MonitorLine);
    }

    [Fact]
    public void UnreadableRefreshRate_IsLeftOut_AndErrorsDoNotEscape()
    {
        var vm = new ShellViewModel("HP P21b G4", ConnectionType.Unknown, () => null, "1.0.0");
        vm.Refresh();
        Assert.Equal("HP P21b G4 · bağlantı bilinmiyor", vm.MonitorLine);

        var broken = new ShellViewModel("HP P21b G4", ConnectionType.Hdmi, () => throw new InvalidOperationException("ekran yok"), "1.0.0");
        var task = broken.Refresh();
        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal("HP P21b G4 · HDMI", broken.MonitorLine);
    }

    /// <summary>Hz okuması servis çağrısıdır: arka plandaki sırada çalışır (T316), sıra bitince satır güncellenir.</summary>
    [Fact]
    public void Refresh_ReadsOnTheLane()
    {
        var lane = new ManualLane();
        var reads = 0;
        var vm = new ShellViewModel("HP P21b G4", ConnectionType.Hdmi, () => { reads++; return 76; }, "1.0.0", lane);

        var task = vm.Refresh();
        Assert.Equal(0, reads);

        lane.RunAll();
        Assert.True(task.IsCompleted);
        Assert.Equal("HP P21b G4 · HDMI · 76 Hz", vm.MonitorLine);
    }

    [Fact]
    public void Texts_AreTranslated()
    {
        TestCulture.With("en-US", () =>
        {
            var vm = new ShellViewModel("HP P21b G4", ConnectionType.Unknown, () => null, "2.0.0");
            Assert.Equal("Version 2.0.0", vm.Version);
            Assert.Equal("HP P21b G4 · connection unknown", vm.MonitorLine);
        });
    }
}
