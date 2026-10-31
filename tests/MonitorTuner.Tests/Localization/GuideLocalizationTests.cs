using System.Xml.Linq;

namespace MonitorTuner.Tests.Localization;

/// <summary>
/// T311: Rehber metinlerinin ve teknik parametrelerinin çeviri dosyasında eksiksiz bulunduğunu test eder.
/// </summary>
public class GuideLocalizationTests
{
    private static readonly string Root = RepoRoot.Find();
    private static readonly string GuideResxPath = Path.Combine(Root, "src", "MonitorTuner.App", "Resources", "Strings", "Guide.resx");

    private static Dictionary<string, string> LoadGuideResx()
    {
        Assert.True(File.Exists(GuideResxPath), $"Guide.resx bulunamadı: {GuideResxPath}");
        var doc = XDocument.Load(GuideResxPath);
        return doc.Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");
    }

    [Fact]
    public void GuideResx_Exists()
    {
        Assert.True(File.Exists(GuideResxPath));
    }

    [Fact]
    public void GuideResx_ContainsAllRequiredSectionKeys()
    {
        var keys = LoadGuideResx();

        string[] requiredKeys =
        [
            "TabTitle",
            "PageTitle",
            "IntroText",
            // 1. Oturma ve Açı (TN)
            "SeatingTitle",
            "SeatingDesc",
            "SeatingDiagnosis",
            // 2. Hz Kurulumu (NVIDIA özel çözünürlük)
            "HzTitle",
            "HzDesc",
            "Hz76Details",
            "Hz75Details",
            "Hz74Details",
            "Hz72Details",
            "Hz60Details",
            "Hz50Details",
            // 3. Kare Atlama ve Stabilite Testi
            "TestTitle",
            "PhotosensitivityWarningTitle",
            "PhotosensitivityWarningText",
            "FrameSkippingDesc",
            "StabilityTestDesc",
            // 4. RTX Video Super Resolution
            "RtxVsrTitle",
            "RtxVsrDesc",
            // 5. DLDSR
            "DldsrTitle",
            "DldsrDesc",
            // 6. ClearType
            "ClearTypeTitle",
            "ClearTypeDesc",
            // 7. Ortam Işığı
            "AmbientLightingTitle",
            "AmbientLightingDesc",
            // 8. 20-20-20 Kuralı
            "Rule202020Title",
            "Rule202020Desc",
            // 9. Windows Gece Işığı / f.lux Çakışması
            "NightLightConflictTitle",
            "NightLightConflictDesc",
        ];

        foreach (var key in requiredKeys)
        {
            Assert.True(keys.ContainsKey(key), $"Guide.resx içinde '{key}' anahtarı eksik.");
            Assert.False(string.IsNullOrWhiteSpace(keys[key]), $"Guide.resx içindeki '{key}' değeri boş olamaz.");
        }
    }

    [Fact]
    public void GuideResx_ContainsEssentialHardwareValuesAndWarnings()
    {
        var keys = LoadGuideResx();
        var allText = string.Join(" ", keys.Values);

        // 00-DONANIM.md ve 08-GORUNTU-YONTEMLERI.md teknik değerleri
        Assert.Contains("76", allText);
        Assert.Contains("1092", allText); // 2080x1092 (76 Hz dikey timing)
        Assert.Contains("82.99", allText); // 76 Hz kHz
        Assert.Contains("172.62", allText); // 76 Hz MHz
        Assert.Contains("CVT-RB", allText); // 74 Hz modu
        Assert.Contains("70", allText); // 70-80 cm mesafe
        Assert.Contains("üst kenar", allText); // Göz hizası
        Assert.Contains("epilepsi", allText, StringComparison.OrdinalIgnoreCase); // Işığa duyarlılık uyarısı
        Assert.Contains("testufo", allText, StringComparison.OrdinalIgnoreCase); // Test sitesi
        Assert.Contains("cttune", allText, StringComparison.OrdinalIgnoreCase); // ClearType aracı
    }
}
