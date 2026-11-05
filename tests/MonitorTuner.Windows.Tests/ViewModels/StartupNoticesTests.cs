using MonitorTuner.App.ViewModels;
using MonitorTuner.Core;
using MonitorTuner.Services;

namespace MonitorTuner.Windows.Tests.ViewModels;

/// <summary>Açılış iletileri (T314): her başarısız adım kullanıcıya söylenir, hata yutulmaz; başarılı açılış sessizdir.</summary>
public class StartupNoticesTests
{
    [Fact]
    public void From_SuccessfulStartup_SaysNothing() =>
        Assert.Empty(StartupNotices.From(new StartupReport(FullGammaRange: true, HasCalibration: true, TableApplied: true)));

    [Fact]
    public void From_DeniedAdminPrompt_ExplainsWhatIsLimited()
    {
        var notice = Assert.Single(StartupNotices.From(new StartupReport(false, false, true)));

        Assert.DoesNotContain("[[", notice, StringComparison.Ordinal);
        Assert.Contains("yönetici", notice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void From_RejectedTable_IsReported()
    {
        var notice = Assert.Single(StartupNotices.From(new StartupReport(true, true, false)));

        Assert.DoesNotContain("[[", notice, StringComparison.Ordinal);
    }

    [Fact]
    public void From_EverythingFailed_GivesEachReasonOnce() =>
        Assert.Equal(2, StartupNotices.From(new StartupReport(false, true, false)).Count);

    [Fact]
    public void ExternalTableRestored_HasText() =>
        Assert.DoesNotContain("[[", StartupNotices.ExternalTableRestored(), StringComparison.Ordinal);

    [Fact]
    public void NoSupportedMonitor_ListsWhatWasSeen_AndSaysNothingWasChanged()
    {
        var seen = new[]
        {
            new MonitorInfo(new DisplayId(@"\\.\DISPLAY1"), "DELL U2412M", "X1", ConnectionType.DisplayPort, "DP", "NVIDIA GeForce RTX 3080"),
            new MonitorInfo(new DisplayId(@"\\.\DISPLAY2"), "", "", ConnectionType.Hdmi, "HDMI", "NVIDIA GeForce RTX 3090"),
        };

        var text = StartupNotices.NoSupportedMonitor(seen);

        Assert.DoesNotContain("[[", text, StringComparison.Ordinal);
        Assert.Contains("DELL U2412M", text, StringComparison.Ordinal);
        Assert.Contains("DISPLAY2", text, StringComparison.Ordinal); // EDID'i okunamayan ekran da görünür
    }

    [Fact]
    public void NoSupportedMonitor_WithNoDisplays_StillHasText() =>
        Assert.DoesNotContain("[[", StartupNotices.NoSupportedMonitor([]), StringComparison.Ordinal);

    [Fact]
    public void ProfileError_ShowsTheReason()
    {
        var text = StartupNotices.ProfileError("şema sürümü");

        Assert.DoesNotContain("[[", text, StringComparison.Ordinal);
        Assert.Contains("şema sürümü", text, StringComparison.Ordinal);
    }
}
