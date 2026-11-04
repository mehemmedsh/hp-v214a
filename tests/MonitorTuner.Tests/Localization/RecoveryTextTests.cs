using System.Text;
using System.Xml.Linq;

namespace MonitorTuner.Tests.Localization;

/// <summary>
/// T104: kurtarma dosyası (<c>tools/recovery</c>) ve sonucunu gösteren Türkçe metinler. Kurtarma dosyası uygulamayı
/// kurtarma kipinde açar; "görüntü gelmezse" talimatı aynı klasörde ve pakette.
/// </summary>
public class RecoveryTextTests
{
    private static readonly string Root = RepoRoot.Find();
    private static readonly string RecoveryFolder = Path.Combine(Root, "tools", "recovery");

    private static Dictionary<string, string> Resx() =>
        XDocument.Load(Path.Combine(Root, "src", "MonitorTuner.App", "Resources", "Strings", "Recovery.resx")).Root!
            .Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");

    /// <summary>Raporun her alanı için "oldu" ve "olmadı" metni var (App.xaml.cs bu anahtarları kullanır).</summary>
    [Fact]
    public void Resx_HasDoneAndFailedTextForEveryReportField()
    {
        var keys = Resx();
        foreach (var field in new[] { "GpuTable", "GammaRange", "Dithering", "Vibrance" })
        foreach (var state in new[] { "Done", "Failed" })
            Assert.False(string.IsNullOrWhiteSpace(keys.GetValueOrDefault($"{field}_{state}")), $"{field}_{state} yok");
        foreach (var key in new[] { "Title", "AllDone", "SomeFailed", "NoDisplay", "Error" })
            Assert.False(string.IsNullOrWhiteSpace(keys.GetValueOrDefault(key)), $"{key} yok");
    }

    /// <summary>Başarısızlıkta kullanıcı talimata ve güvenli moddaki NVIDIA sınırına yönlendirilir.</summary>
    [Fact]
    public void FailureText_PointsToInstructions_AndSafeModeNvidiaLimit()
    {
        var keys = Resx();
        Assert.Contains(InstructionsName, keys["SomeFailed"]);
        Assert.Contains("güvenli mod", keys["Dithering_Failed"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("yönetici", keys["GammaRange_Failed"], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>.cmd ASCII'dir (cmd.exe kod sayfası Türkçe harfleri bozar), CRLF ile çıkar ve uygulamayı kurtarma kipinde açar.</summary>
    [Fact]
    public void RecoveryCmd_IsAscii_AndStartsAppInRecoveryMode()
    {
        var path = Path.Combine(RecoveryFolder, "Kurtarma.cmd");
        var bytes = File.ReadAllBytes(path);

        Assert.All(bytes, b => Assert.True(b < 0x80, $"ASCII dışı bayt: {b:X2}"));
        var text = Encoding.ASCII.GetString(bytes);
        Assert.Contains("MonitorTuner.App.exe", text);
        Assert.Contains("--kurtarma", text);
        Assert.Contains("*.cmd text eol=crlf", File.ReadAllText(Path.Combine(Root, ".gitattributes")));
    }

    private const string InstructionsName = "GORUNTU-GELMEZSE.txt";

    /// <summary>"Görüntü gelmezse" talimatı: bekleme, başka port, güvenli mod + kurtarma dosyası, NVIDIA özel çözünürlüğünü silme.</summary>
    [Fact]
    public void Instructions_CoverWaitOtherPortSafeModeAndCustomResolution()
    {
        var text = File.ReadAllText(Path.Combine(RecoveryFolder, InstructionsName));

        foreach (var expected in new[] { "15 saniye", "3090", "DisplayPort", "Güvenli mod", "Kurtarma.cmd", "Özel çözünürlük", "Kaldır", "60 Hz" })
            Assert.Contains(expected, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EDID override'larını siler", text); // EDID'ye yazılmaz (03-GUVENLIK.md → 1.2)
    }

    /// <summary>Kurtarma dosyası ve talimat paketin köküne, exe'nin yanına kopyalanır.</summary>
    [Fact]
    public void AppProject_CopiesRecoveryFolderNextToExe()
    {
        var csproj = File.ReadAllText(Path.Combine(Root, "src", "MonitorTuner.App", "MonitorTuner.App.csproj"));

        Assert.Contains(@"..\..\tools\recovery\", csproj);
        Assert.Contains("CopyToPublishDirectory", csproj);
    }
}
