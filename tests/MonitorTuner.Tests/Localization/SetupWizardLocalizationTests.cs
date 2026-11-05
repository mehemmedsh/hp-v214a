using System.Xml.Linq;
using MonitorTuner.Services;

namespace MonitorTuner.Tests.Localization;

/// <summary>T312: sihirbazın Türkçe metinleri eksiksiz; her adımın başlığı ve yönergesi var.</summary>
public class SetupWizardLocalizationTests
{
    private static readonly string ResxPath =
        Path.Combine(RepoRoot.Find(), "src", "MonitorTuner.App", "Resources", "Strings", "SetupWizard.resx");

    private static Dictionary<string, string> Load() =>
        XDocument.Load(ResxPath).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");

    [Fact]
    public void EveryStep_HasTitleAndInstruction()
    {
        var keys = Load();

        foreach (var step in Enum.GetValues<WizardStep>()) // T315: gamma ve gri dengesi dahil
        {
            Assert.False(string.IsNullOrWhiteSpace(keys.GetValueOrDefault($"Step_{step}_Title")), $"{step}: başlık yok");
            Assert.False(string.IsNullOrWhiteSpace(keys.GetValueOrDefault($"Step_{step}_Text")), $"{step}: yönerge yok");
        }
    }

    [Fact]
    public void WarmUpStep_RemindsThirtyMinutesAndPosition()
    {
        var text = Load()["Step_WarmUpAndPosition_Text"];

        Assert.Contains("30 dakika", text);
        Assert.Contains("üst kenarı", text);
    }

    [Fact]
    public void WhiteMatch_OnlyLowersGains_AndNeedsCustomRgb()
    {
        var text = Load()["Step_WhiteMatch_Text"];

        Assert.Contains("azalt", text);
        Assert.Contains("Custom RGB", text);
        Assert.Contains("Pixel", text);
    }

    /// <summary>T315: sayfadaki desen düğmeleri, gezinme, ısınma sayacı, gamma, gri dengesi ve sonuç iletileri.</summary>
    [Fact]
    public void PageStrings_Exist()
    {
        var keys = Load();
        string[] page =
        [
            "Start", "Back", "Next", "Finish", "Cancel", "StepCounter", "WarmUpRemaining", "WarmUpDone", "OpenPattern",
            "MonitorSettings", "NoDdc", "GpuWhite_Title", "GpuWhite_Text", "Gamma_Label", "Gamma_Choice", "Gamma_LastFrame",
            "Grey_Dark", "Grey_Mid", "Grey_Light", "Channel_R", "Channel_G", "Channel_B", "GainItem",
            "Status_Started", "Status_Finished", "Status_Cancelled", "Status_CancelPartial",
            "Status_CalibrationApplied", "Status_CalibrationRejected", "Status_CalibrationOutOfRange", "Idle",
        ];
        var missing = page.Concat(Enum.GetValues<PatternKind>().Select(k => $"Pattern_{k}"))
            .Where(k => string.IsNullOrWhiteSpace(keys.GetValueOrDefault(k))).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void FormatStrings_HaveTheirPlaceholders()
    {
        var keys = Load();

        Assert.Contains("{0}", keys["StepCounter"]);
        Assert.Contains("{1}", keys["StepCounter"]);
        Assert.Contains("{0}", keys["WarmUpRemaining"]);
        Assert.Contains("{0}", keys["OpenPattern"]);
        Assert.Contains("{0}", keys["Gamma_Choice"]);
        Assert.Contains("{1}", keys["Gamma_Choice"]);
        Assert.Contains("{0}", keys["Gamma_LastFrame"]);
        Assert.Contains("{0}", keys["GainItem"]);
        Assert.Contains("{1}", keys["GainItem"]);
        Assert.Contains("{0}", keys["Status_CancelPartial"]);
    }

    [Fact]
    public void PatternWindowStrings_Exist()
    {
        var keys = Load();

        Assert.False(string.IsNullOrWhiteSpace(keys.GetValueOrDefault("SampleText")));
        Assert.Contains("Esc", keys["PatternKeys"]);
    }
}
