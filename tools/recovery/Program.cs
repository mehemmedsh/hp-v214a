using System.Text;
using MonitorTuner.Composition;

// Kurtarma dosyası (T104). Ne yaptığı ve "görüntü gelmezse" adımları: OKU.md.
Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("MonitorTuner kurtarma");
Console.WriteLine("GPU renk tablosu sıfırlanır, dithering ve canlılık NVIDIA varsayılanına, GdiIcmGammaRange eski hâline döner.");
Console.WriteLine("Hz, parlaklık ve monitör (DDC) ayarlarına dokunulmaz. Yönetici onayı penceresi çıkarsa \"Evet\" de.");
Console.WriteLine();

const string NoNvidia = "NVIDIA sürücüsü cevap vermedi (güvenli modda yüklenmez). Normal modda bu dosyayı bir kez daha çalıştır.";
var backup = RecoveryComposition.HasFactoryBackup();
var results = RecoveryComposition.ResetAllDisplays();
var ok = results.Count > 0;
if (results.Count == 0) Console.WriteLine("Masaüstünde ekran bulunamadı; hiçbir şey yapılmadı.");

for (var i = 0; i < results.Count; i++)
{
    var (display, report) = results[i];
    Console.WriteLine($"Ekran {display.Value}");
    ok &= Line("GPU renk tablosu (birim tablo)", report.GpuTableReset,
        "Tablo okunamadı ya da Windows reddetti. Güvenli moddaysan normal; tablo Windows yeniden başlayınca da sıfırlanır.");
    ok &= Line("Dithering (NVIDIA varsayılanı)", report.DitheringReset, NoNvidia);
    ok &= Line("Canlılık (%50, NVIDIA varsayılanı)", report.VibranceReset, NoNvidia);
    if (i == 0) // tek kayıt, bütün ekranlar için
    {
        ok &= Line(backup ? "GdiIcmGammaRange (eski hâli)" : "GdiIcmGammaRange (dokunulmadı: uygulama hiç değiştirmemiş)",
            report.GammaRangeRestored, "Yönetici onayı verilmedi ya da kayıt yazılamadı. Dosyayı tekrar çalıştırıp \"Evet\" de.");
    }
    Console.WriteLine();
}

Console.WriteLine(ok ? "Bitti: her şey varsayılanda." : "Bazı adımlar yapılamadı; yukarıdaki notlara bak.");
if (!Console.IsInputRedirected)
{
    Console.WriteLine("Kapatmak için bir tuşa bas.");
    Console.ReadKey(intercept: true);
}
return ok ? 0 : 1;

static bool Line(string what, bool success, string hint)
{
    Console.WriteLine(success ? $"  [tamam] {what}" : $"  [YAPILAMADI] {what}. {hint}");
    return success;
}
