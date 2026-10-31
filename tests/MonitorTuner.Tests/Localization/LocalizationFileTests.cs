using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MonitorTuner.Tests.Localization;

/// <summary>
/// Dil altyapısının dosya düzeyindeki kuralları (Linux'ta da çalışır): arayüz metinleri sadece
/// çeviri dosyalarından gelir, özellik başına ayrı .resx, nötr dil Türkçe.
/// </summary>
public partial class LocalizationFileTests
{
    private static readonly string Root = RepoRoot.Find();
    private static readonly string StringsDir = Path.Combine(Root, "src", "MonitorTuner.App", "Resources", "Strings");
    private static readonly string[] TextAttributes = ["Text", "Content", "Title", "Header", "ToolTip", "Label", "Description"];

    [Fact]
    public void NeutralTurkishCommonFile_Exists() =>
        Assert.True(File.Exists(Path.Combine(StringsDir, "Common.resx")));

    [Fact]
    public void Xaml_HasNoHardcodedText()
    {
        var found = XamlFiles().SelectMany(f => FindHardcodedText(XDocument.Load(f)).Select(t => $"{f}: {t}")).ToList();

        Assert.Empty(found);
    }

    [Fact]
    public void HardcodedTextCheck_CatchesLiteral()
    {
        const string xaml = """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Title="Merhaba">
              <TextBlock Text="{loc:Tr Common.AppTitle}" />
              <Button Content="" />
            </Window>
            """;

        Assert.Equal(["Window.Title=\"Merhaba\""], FindHardcodedText(XDocument.Parse(xaml)));
    }

    [Fact]
    public void XamlKeys_ExistInNeutralResx()
    {
        var keys = XamlFiles().SelectMany(f => TrKey().Matches(File.ReadAllText(f)).Select(m => (m.Groups[1].Value, m.Groups[2].Value))).ToList();
        Assert.NotEmpty(keys);

        var missing = keys.Where(k => !ResxKeys(Path.Combine(StringsDir, k.Item1 + ".resx")).ContainsKey(k.Item2))
            .Select(k => $"{k.Item1}.{k.Item2}").ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void NeutralResxValues_AreNotEmpty()
    {
        var empty = NeutralResxFiles().SelectMany(f => ResxKeys(f).Where(kv => string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => $"{Path.GetFileName(f)}: {kv.Key}")).ToList();

        Assert.Empty(empty);
    }

    [Fact]
    public void TranslationFiles_HaveNeutralCounterpart_AndNoExtraKeys()
    {
        foreach (var file in Directory.GetFiles(StringsDir, "*.*.resx"))
        {
            var neutral = Path.Combine(StringsDir, Path.GetFileName(file).Split('.')[0] + ".resx");
            Assert.True(File.Exists(neutral), $"Nötr (Türkçe) dosya yok: {neutral}");
            Assert.Empty(ResxKeys(file).Keys.Except(ResxKeys(neutral).Keys));
        }
    }

    internal static IReadOnlyList<string> FindHardcodedText(XDocument xaml) =>
        xaml.Descendants().SelectMany(e => e.Attributes()
                .Where(a => TextAttributes.Contains(a.Name.LocalName) && a.Value.Trim().Length > 0 && !a.Value.TrimStart().StartsWith('{'))
                .Select(a => $"{e.Name.LocalName}.{a.Name.LocalName}=\"{a.Value}\""))
            .ToList();

    private static IEnumerable<string> XamlFiles() =>
        Directory.GetFiles(Path.Combine(Root, "src"), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static IEnumerable<string> NeutralResxFiles() =>
        Directory.GetFiles(StringsDir, "*.resx").Where(f => Path.GetFileName(f).Count(c => c == '.') == 1);

    private static Dictionary<string, string> ResxKeys(string path) =>
        File.Exists(path)
            ? XDocument.Load(path).Root!.Elements("data")
                .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "")
            : [];

    [GeneratedRegex(@"\{loc:Tr\s+(?:Key=)?([A-Za-z0-9_]+)\.([A-Za-z0-9_]+)\s*\}")]
    private static partial Regex TrKey();
}
