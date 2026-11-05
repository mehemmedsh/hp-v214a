using MonitorTuner.Services;

namespace MonitorTuner.Tests.Services;

/// <summary>T502: arayüz dili seçimi. Varsayılan Türkçe (Windows'un dili ne olursa olsun); seçim bir dosyada saklanır.</summary>
public sealed class LanguagePreferenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mt-lang-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Supported_IsTurkishAzerbaijaniEnglish_AndDefaultIsTurkish()
    {
        Assert.Equal(["tr", "az", "en"], LanguagePreference.Supported);
        Assert.Equal("tr", LanguagePreference.Default);
    }

    [Fact]
    public void Load_WithoutFile_IsTurkish_AndCreatesNothing()
    {
        Assert.Equal("tr", LanguagePreference.Load(_root));
        Assert.False(Directory.Exists(_root));
    }

    [Theory]
    [InlineData("az")]
    [InlineData("en")]
    [InlineData("tr")]
    public void Save_ThenLoad_RoundTrips(string code)
    {
        LanguagePreference.Save(_root, code);

        Assert.Equal(code, LanguagePreference.Load(_root));
    }

    [Theory]
    [InlineData(" EN \n", "en")]
    [InlineData("de", "tr")]
    [InlineData("", "tr")]
    [InlineData("çöp", "tr")]
    public void Load_ToleratesHandEditedFile_AndFallsBackToTurkish(string content, string expected)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, LanguagePreference.FileName), content);

        Assert.Equal(expected, LanguagePreference.Load(_root));
    }

    [Fact]
    public void Save_RefusesUnsupportedLanguage_AndWritesNothing()
    {
        Assert.Throws<ArgumentException>(() => LanguagePreference.Save(_root, "de"));
        Assert.False(File.Exists(Path.Combine(_root, LanguagePreference.FileName)));
    }
}
