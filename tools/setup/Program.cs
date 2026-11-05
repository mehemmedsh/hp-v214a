using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;

namespace MonitorTuner.Setup;

/// <summary>
/// Kurulum dosyası (T317). Çift tıkla: ilk kurulumda soru sormaz, kurar, masaüstüne kısayol koyar ve uygulamayı açar.
/// Zaten kuruluysa güncellemek mi kaldırmak mı istediğini sorar. Ne yaptığı: OKU.txt.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class Program
{
    private const string AppProcess = "MonitorTuner.App";

    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("MonitorTuner kurulumu");
        Console.WriteLine();

        var installer = new Installer(InstallPaths.ForCurrentUser(AppContext.BaseDirectory), new ShellShortcutWriter());
        try
        {
            var uninstall = args.Contains("--kaldir", StringComparer.OrdinalIgnoreCase);
            if (!uninstall && installer.State == InstallState.Installed)
            {
                Console.WriteLine("MonitorTuner zaten kurulu.");
                Console.WriteLine("  Enter: güncelle (yeni sürümü kur)   K: kaldır   Esc: çık");
                var key = ReadKey();
                if (key == ConsoleKey.Escape) return 0;
                uninstall = key == ConsoleKey.K;
            }

            WaitForAppToExit();
            if (uninstall)
            {
                installer.Uninstall();
                Console.WriteLine("Kaldırıldı. Ayarların ve yedeklerin %AppData%\\MonitorTuner'da duruyor (yeniden kurunca geri gelir).");
                Pause();
                return 0;
            }

            Console.WriteLine("Kuruluyor: " + installer.Paths.Target);
            installer.Install();
            Console.WriteLine("Masaüstüne ve Başlat menüsüne \"MonitorTuner\" kısayolu kondu.");
            Console.WriteLine("Kurulum bitti. Uygulama açılıyor...");
            Process.Start(new ProcessStartInfo(installer.Paths.AppExe) { UseShellExecute = true, WorkingDirectory = installer.Paths.Target });
            Thread.Sleep(1500);
            return 0;
        }
        catch (Exception ex) when (ex is InstallException or IOException or UnauthorizedAccessException)
        {
            Console.WriteLine();
            Console.WriteLine("YAPILAMADI: " + ex.Message);
            Console.WriteLine("Uygulama açıksa tepsideki simgeden Çıkış de ve kurulumu tekrar çalıştır.");
            Pause();
            return 1;
        }
    }

    /// <summary>
    /// Açık uygulama kapatılmaz (zorla kapatmak efektleri ekranda bırakırdı); kullanıcı tepsiden çıkana kadar beklenir.
    /// </summary>
    private static void WaitForAppToExit()
    {
        var told = false;
        while (IsAppRunning())
        {
            if (!told)
            {
                Console.WriteLine("Uygulama açık. Saatin yanındaki MonitorTuner simgesine sağ tıkla → Çıkış. Kapanınca kurulum kendiliğinden devam eder.");
                told = true;
            }

            Thread.Sleep(1000);
        }
    }

    private static bool IsAppRunning()
    {
        var running = Process.GetProcessesByName(AppProcess);
        foreach (var p in running) p.Dispose();
        return running.Length > 0;
    }

    private static ConsoleKey ReadKey() => Console.IsInputRedirected ? ConsoleKey.Enter : Console.ReadKey(intercept: true).Key;

    private static void Pause()
    {
        if (Console.IsInputRedirected) return;
        Console.WriteLine("Kapatmak için bir tuşa bas.");
        Console.ReadKey(intercept: true);
    }
}
