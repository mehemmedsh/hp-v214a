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
    public void EveryT312Step_HasTitleAndInstruction()
    {
        var keys = Load();
        var steps = Enum.GetValues<WizardStep>().Except([WizardStep.Gamma, WizardStep.GreyBalance]); // T313

        foreach (var step in steps)
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

    [Fact]
    public void PatternWindowStrings_Exist()
    {
        var keys = Load();

        Assert.False(string.IsNullOrWhiteSpace(keys.GetValueOrDefault("SampleText")));
        Assert.Contains("Esc", keys["PatternKeys"]);
    }
}
