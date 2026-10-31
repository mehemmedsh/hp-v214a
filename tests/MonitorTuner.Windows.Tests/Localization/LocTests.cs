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
        // Azerbaycanca ve İngilizce dosyalar T502'de gelecek; o zamana kadar nötr dil (Türkçe) görünür.
        Assert.Equal("Monitör Ayarı", Loc.Get("Common.AppTitle", CultureInfo.GetCultureInfo("en-US")));
    }

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
