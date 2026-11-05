using System.Globalization;
using System.Runtime.CompilerServices;

namespace MonitorTuner.Windows.Tests;

/// <summary>
/// T502: Azerbaycanca ve İngilizce çeviriler geldikten sonra testler Türkçe metinleri bekler. CI'daki Windows'un arayüz dili
/// İngilizce olduğundan, test süreci arayüz dilini Türkçeye (uygulamanın varsayılan dili) sabitler. Başka dili deneyen test
/// dili kendisi verir ve geri alır.
/// </summary>
internal static class TestCulture
{
    public static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    [ModuleInitializer]
    internal static void UseTurkish()
    {
        CultureInfo.DefaultThreadCurrentUICulture = Turkish;
        CultureInfo.CurrentUICulture = Turkish;
    }

    /// <summary>Gövde boyunca arayüz dilini değiştirir.</summary>
    public static void With(string culture, Action body)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            body();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
