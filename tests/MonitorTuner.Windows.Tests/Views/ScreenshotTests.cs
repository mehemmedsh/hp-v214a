using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MonitorTuner.App;
using MonitorTuner.App.ViewModels;
using MonitorTuner.Core;
using MonitorTuner.Services;

namespace MonitorTuner.Windows.Tests.Views;

/// <summary>
/// Arayüzün görüntüsü (T322). Bulut oturumu WPF penceresini göremez: CI, <c>MT_SCREENSHOTS</c> klasörü verince ana pencerenin
/// sayfalarını açık ve koyu temada PNG (tam boy) ve küçük JPEG (günlüğe yazılır) olarak kaydeder. Değişken yoksa hiçbir şey
/// yapmaz (yerelde ve normal test koşusunda pencere açılmaz). Donanım yok: sahte modlar ve sabit monitör satırı.
/// </summary>
public class ScreenshotTests
{
    private sealed class StubQuick : IQuickControls
    {
        public bool Enabled => true;
        public IReadOnlyList<ColorProfile> Profiles { get; } = [.. ColorProfiles.BuiltIns, new("Benim", false, 0.45, 5000, null)];
        public ActiveMode? Active { get; set; } = new("Sinema", Adjusted: false);
        public QuickResult SelectProfile(string name) => new(QuickOutcome.Done);
        public QuickResult SetBrightness(double slider) => new(QuickOutcome.Done);
        public QuickResult SetBlueLight(int kelvin) => new(QuickOutcome.Done);
        public QuickResult Panic() => new(QuickOutcome.Done);
    }

    [Fact]
    public void MainWindowPages_AreCaptured_WhenAskedFor()
    {
        var folder = Environment.GetEnvironmentVariable("MT_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(folder)) return;
        Directory.CreateDirectory(folder);

        RunOnSta(() =>
        {
#pragma warning disable WPF0001 // ThemeMode (Fluent tema) .NET 10'da deneysel API; sadece bu testte açık/koyu seçmek için
            foreach (var (theme, name) in new[] { (ThemeMode.Light, "acik"), (ThemeMode.Dark, "koyu") })
            {
                Capture(folder, name, "Modes", window => window.ThemeMode = theme);
                Capture(folder, name, "Settings", window => window.ThemeMode = theme);
            }
#pragma warning restore WPF0001
        });
    }

    private static void Capture(string folder, string themeName, string page, Action<MainWindow> setTheme)
    {
        var window = new MainWindow
        {
            ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = 0,
            Top = 0,
        };
        setTheme(window);

        var modes = new ModesViewModel(new StubQuick());
        var shell = new ShellViewModel("HP P21b G4", ConnectionType.Hdmi, () => 76, "1.0.0");
        shell.Refresh();
        ((FrameworkElement)window.FindName("ModesPanel")).DataContext = modes;
        ((FrameworkElement)window.FindName("ShellPanel")).DataContext = shell;
        ((FrameworkElement)window.FindName("SettingsPage")).DataContext = shell;
        ((ListBox)window.FindName("Nav")).SelectedItem = window.FindName("Nav" + page);

        window.Show();
        DoEvents();

        var root = (Panel)window.Content;
        // Görüntüde Mica (saydam arka plan) olmaz: temanın düz arka planı verilir.
        root.Background = window.TryFindResource("SolidBackgroundFillColorBaseBrush") as Brush ?? Brushes.Gray;
        root.UpdateLayout();
        DoEvents();

        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        window.Close();

        var baseName = Path.Combine(folder, $"{page.ToLowerInvariant()}-{themeName}");
        Save(new PngBitmapEncoder(), BitmapFrame.Create(bitmap), baseName + ".png");
        Save(new JpegBitmapEncoder { QualityLevel = 72 }, BitmapFrame.Create(new TransformedBitmap(bitmap, new ScaleTransform(0.62, 0.62))),
            baseName + ".jpg");
    }

    private static void Save(BitmapEncoder encoder, BitmapFrame frame, string path)
    {
        encoder.Frames.Add(frame);
        using var file = File.Create(path);
        encoder.Save(file);
    }

    /// <summary>Bekleyen yerleşim ve çizim işleri bitsin.</summary>
    private static void DoEvents()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
            new DispatcherOperationCallback(f =>
            {
                ((DispatcherFrame)f!).Continue = false;
                return null;
            }), frame);
        Dispatcher.PushFrame(frame);
    }

    private static void RunOnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) throw new InvalidOperationException("STA thread hatası", error);
    }
}
