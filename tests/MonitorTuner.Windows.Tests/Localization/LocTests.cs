using System.Globalization;
using System.Reflection;
using System.Resources;
using MonitorTuner.App.Localization;

namespace MonitorTuner.Windows.Tests.Localization;

public class LocTests
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    [Fact]
    public void Get_ReturnsTurkishText()
    {
        Assert.Equal("Monitör Ayarı", Loc.Get("Common.AppTitle", Turkish));
    }

    [Fact]
    public void Get_FallsBackToTurkish_WhenTranslationIsMissing()
    {
        // Almanca çeviri yok: nötr dil (Türkçe) görünür.
        Assert.Equal("Monitör Ayarı", Loc.Get("Common.AppTitle", CultureInfo.GetCultureInfo("de-DE")));
    }

    /// <summary>T502: Azerbaycanca ve İngilizce çeviri dosyaları derlemeye uydu derlemesi olarak girer ve okunur.</summary>
    [Theory]
    [InlineData("az", "Common.AppTitle", "Monitor Ayarı")]
    [InlineData("az-Latn-AZ", "Tray.Exit", "Çıxış")]
    [InlineData("en", "Common.AppTitle", "Monitor Tuner")]
    [InlineData("en-US", "Tray.Exit", "Exit")]
    [InlineData("en-GB", "SetupWizard.Next", "Next")]
    public void Get_ReturnsTranslation(string culture, string key, string expected) =>
        Assert.Equal(expected, Loc.Get(key, CultureInfo.GetCultureInfo(culture)));

    [Fact]
    public void Get_UsesCurrentUICulture_ByDefault()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = Turkish;
            Assert.Equal("Monitör Ayarı", Loc.Get("Common.AppTitle"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Theory]
    [InlineData("Common.NoSuchKey")]
    [InlineData("NoSuchFeature.AppTitle")]
    public void Get_ShowsMarker_ForMissingText(string key)
    {
        Assert.Equal($"[[{key}]]", Loc.Get(key, Turkish));
    }

    [Theory]
    [InlineData("")]
    [InlineData("AppTitle")]
    [InlineData(".AppTitle")]
    [InlineData("Common.")]
    [InlineData("Common.App.Title")]
    public void Get_RejectsMalformedKey(string key)
    {
        Assert.Throws<ArgumentException>(() => Loc.Get(key, Turkish));
    }

    [Fact]
    public void AppAssembly_NeutralLanguageIsTurkish()
    {
        var attribute = typeof(Loc).Assembly.GetCustomAttribute<NeutralResourcesLanguageAttribute>();

        Assert.Equal("tr", attribute?.CultureName);
    }
}
