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

        // Her Hz anahtarı için 00-DONANIM.md değerleri ayrı ayrı doğrulanır (metin birleştirmesi yapılmaz)
        var hz76 = keys["Hz76Details"];
        Assert.Contains("2080x1092", hz76);
        Assert.Contains("48/32/80", hz76);
        Assert.Contains("3/5/4", hz76);
        Assert.Contains("82.99", hz76);
        Assert.Contains("172.62", hz76);
        Assert.Contains("En Yüksek Hız", hz76);
        Assert.Contains("NVIDIA testi geçti; kullanmadan önce kare atlama testiyle doğrulayın", hz76);

        var hz75 = keys["Hz75Details"];
        Assert.Contains("2080x1100", hz75);
        Assert.Contains("3/5/12", hz75);
        Assert.Contains("82.50", hz75);
        Assert.Contains("171.60", hz75);
        Assert.Contains("NVIDIA testi geçti; kullanmadan önce kare atlama testiyle doğrulayın", hz75);

        var hz74 = keys["Hz74Details"];
        Assert.Contains("2080x1119", hz74);
        Assert.Contains("82.81", hz74);
        Assert.Contains("172.24", hz74);
        Assert.Contains("Doğrulanmış", hz74);

        var hz60 = keys["Hz60Details"];
        Assert.Contains("2200x1125", hz60);
        Assert.Contains("67.5", hz60);
        Assert.Contains("148.5", hz60);

        var hz50 = keys["Hz50Details"];
        Assert.Contains("Monitörün EDID tablosunda hazır bulunan 50 Hz modu", hz50);
        Assert.DoesNotContain("VIC", hz50);
        Assert.DoesNotContain("CTA-861", hz50);

        // Donanım ergonomisi ve güvenlik uyarıları
        Assert.Contains("üst kenar", keys["SeatingDesc"]);
        Assert.Contains("epilepsi", keys["PhotosensitivityWarningText"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cttune", keys["ClearTypeDesc"], StringComparison.OrdinalIgnoreCase);

        // Dokümanda yer almayan değerler ("25/50", "+/+", "1/8", "VIC", "CTA-861") metinde yok; "1/10" var
        var allValues = string.Join(" ", keys.Values);
        Assert.DoesNotContain("25/50", allValues);
        Assert.DoesNotContain("+/+", allValues);
        Assert.DoesNotContain("1/8", allValues);
        Assert.DoesNotContain("VIC", allValues);
        Assert.DoesNotContain("CTA-861", allValues);

        // Önceki incelemede metinden kaldırılan, dokümanda karşılığı olmayan değerler geri gelmemeli
        string[] removedValues = ["8–10", "Kalite 3", "2.25x", "1.78x", "5 adımda", "20 fit", "6 metre"];
        foreach (var removed in removedValues)
        {
            Assert.DoesNotContain(removed, allValues);
        }

        var frameSkipping = keys["FrameSkippingDesc"];
        Assert.Contains("1/10", frameSkipping);
        Assert.Contains("testufo", frameSkipping, StringComparison.OrdinalIgnoreCase);

        // XAML sırası: Işığa duyarlılık uyarısı kare atlama ve hareket kararlılığı testinden önce gelir
        VerifyPhotosensitivityWarningPrecedesTests();
    }

    [Fact]
    public void GuideViewXaml_PhotosensitivityWarningPrecedesStabilityTests()
    {
        VerifyPhotosensitivityWarningPrecedesTests();
    }

    private static void VerifyPhotosensitivityWarningPrecedesTests()
    {
        var guideXamlPath = Path.Combine(Root, "src", "MonitorTuner.App", "Views", "GuideView.xaml");
        Assert.True(File.Exists(guideXamlPath), $"GuideView.xaml bulunamadı: {guideXamlPath}");

        var xaml = File.ReadAllText(guideXamlPath);
        int warningPos = xaml.IndexOf("Guide.PhotosensitivityWarningTitle", StringComparison.Ordinal);
        int frameSkippingPos = xaml.IndexOf("Guide.FrameSkippingDesc", StringComparison.Ordinal);
        int stabilityPos = xaml.IndexOf("Guide.StabilityTestDesc", StringComparison.Ordinal);

        Assert.True(warningPos >= 0, "PhotosensitivityWarningTitle XAML içinde bulunamadı.");
        Assert.True(frameSkippingPos >= 0, "FrameSkippingDesc XAML içinde bulunamadı.");
        Assert.True(stabilityPos >= 0, "StabilityTestDesc XAML içinde bulunamadı.");

        Assert.True(warningPos < frameSkippingPos, "Işığa duyarlılık uyarısı kare atlama testinden önce yer almalıdır.");
        Assert.True(warningPos < stabilityPos, "Işığa duyarlılık uyarısı stabilite testinden önce yer almalıdır.");
    }
}
