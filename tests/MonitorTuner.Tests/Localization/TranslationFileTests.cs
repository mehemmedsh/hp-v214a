using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MonitorTuner.Tests.Localization;

/// <summary>
/// T502 "Tüm ekranlar üç dilde": her Türkçe (nötr) dosyanın Azerbaycanca ve İngilizce karşılığı var, her anahtar çevrilmiş,
/// biçim yer tutucuları ({0}, {1}) aynı. Eksik çeviri uygulamada Türkçeye düşerdi; bu test onu derlemeden önce yakalar.
/// </summary>
public partial class TranslationFileTests
{
    private static readonly string StringsDir = Path.Combine(RepoRoot.Find(), "src", "MonitorTuner.App", "Resources", "Strings");

    public static TheoryData<string, string> FilesAndLanguages()
    {
        var data = new TheoryData<string, string>();
        foreach (var file in NeutralFiles())
        foreach (var language in new[] { "az", "en" })
            data.Add(Path.GetFileNameWithoutExtension(file), language);
        return data;
    }

    [Theory]
    [MemberData(nameof(FilesAndLanguages))]
    public void EveryKey_IsTranslated(string feature, string language)
    {
        var neutral = Load(Path.Combine(StringsDir, feature + ".resx"));
        var path = Path.Combine(StringsDir, $"{feature}.{language}.resx");
        Assert.True(File.Exists(path), $"Çeviri dosyası yok: {feature}.{language}.resx");
        var translated = Load(path);

        Assert.Empty(neutral.Keys.Except(translated.Keys));
        Assert.Empty(translated.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key));
    }

    [Theory]
    [MemberData(nameof(FilesAndLanguages))]
    public void Placeholders_AreKept(string feature, string language)
    {
        var neutral = Load(Path.Combine(StringsDir, feature + ".resx"));
        var translated = Load(Path.Combine(StringsDir, $"{feature}.{language}.resx"));

        var broken = neutral.Where(kv => translated.TryGetValue(kv.Key, out var t) && !Placeholders(kv.Value).SetEquals(Placeholders(t)))
            .Select(kv => kv.Key).ToList();

        Assert.Empty(broken);
    }

    /// <summary>Çeviri gerçekten yapılmış: dosyanın metinlerinin çoğu Türkçeden farklı (sayı, kısaltma gibi aynı kalanlar olabilir).</summary>
    [Theory]
    [MemberData(nameof(FilesAndLanguages))]
    public void Texts_AreActuallyTranslated(string feature, string language)
    {
        var neutral = Load(Path.Combine(StringsDir, feature + ".resx"));
        var translated = Load(Path.Combine(StringsDir, $"{feature}.{language}.resx"));

        var texts = neutral.Where(kv => !kv.Key.StartsWith("Language_", StringComparison.Ordinal)).ToList(); // dil adları her dilde aynı
        var same = texts.Count(kv => translated.GetValueOrDefault(kv.Key) == kv.Value);

        Assert.True(same * 4 <= texts.Count, $"{feature}.{language}: {same}/{texts.Count} metin Türkçeyle aynı");
    }

    [Fact]
    public void LanguageNames_AreTheSameInEveryLanguage()
    {
        // Dil seçicide her dil kendi adıyla görünür: yanlış dili seçen kullanıcı geri dönebilsin.
        foreach (var language in new[] { "az", "en" })
        {
            var translated = Load(Path.Combine(StringsDir, $"Common.{language}.resx"));
            Assert.Equal("Türkçe", translated["Language_tr"]);
            Assert.Equal("Azərbaycanca", translated["Language_az"]);
            Assert.Equal("English", translated["Language_en"]);
        }
    }

    [Theory]
    [InlineData("az")]
    [InlineData("en")]
    public void SafetyCriticalWords_SurviveTranslation(string language)
    {
        // Desen penceresinin tuşu ve OSD'deki ön ayar adı monitörde/klavyede yazdığı gibi kalmalı.
        var wizard = Load(Path.Combine(StringsDir, $"SetupWizard.{language}.resx"));
        Assert.Contains("Esc", wizard["PatternKeys"]);
        Assert.Contains("Custom RGB", wizard["Step_WhiteMatch_Text"]);
        Assert.Contains("Pixel", wizard["Step_WhiteMatch_Text"]);

        var tray = Load(Path.Combine(StringsDir, $"Tray.{language}.resx"));
        Assert.Contains("{0}", tray["Panic"]);
    }

    private static IEnumerable<string> NeutralFiles() =>
        Directory.GetFiles(StringsDir, "*.resx").Where(f => Path.GetFileName(f).Count(c => c == '.') == 1).Order();

    private static Dictionary<string, string> Load(string path) =>
        XDocument.Load(path).Root!.Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");

    private static HashSet<string> Placeholders(string text) => [.. Placeholder().Matches(text).Select(m => m.Value)];

    [GeneratedRegex(@"\{\d+[^}]*\}")]
    private static partial Regex Placeholder();
}
