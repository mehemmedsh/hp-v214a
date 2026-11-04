using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MonitorTuner.App.Localization;
using MonitorTuner.Services;

namespace MonitorTuner.App.Views;

/// <summary>
/// Ayar sihirbazı deseni (T312): tam ekran, ayırt edici kenarlıksız. Sadece <see cref="Pattern"/> verisini çizer; mantık yok.
/// Kareleri kullanıcı gezer (sağ/sol ok, Boşluk); kendiliğinden değişmez ve yanıp sönmez (03-GUVENLIK.md → 7). Esc kapatır.
/// Çizim piksel hizalıdır (yamalar tam piksele yuvarlanır). Hangi ekranda açılacağı çağıranın işi (Left/Top ayarlanır).
/// </summary>
public sealed class PatternWindow : Window
{
    private readonly Pattern _pattern;
    private readonly Canvas _canvas = new() { SnapsToDevicePixels = true };
    private int _frame;

    public PatternWindow(Pattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.Frames.Count == 0) throw new ArgumentException("Desenin kare listesi boş.", nameof(pattern));
        _pattern = pattern;

        WindowStyle = WindowStyle.None;
        WindowState = WindowState.Maximized;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Content = _canvas;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        SizeChanged += (_, _) => Render();
        Loaded += (_, _) => Render();
        KeyDown += OnKeyDown;
    }

    /// <summary>Gösterilen karenin sırası (0 tabanlı).</summary>
    public int FrameIndex => _frame;

    public void Move(int delta)
    {
        _frame = Math.Clamp(_frame + delta, 0, _pattern.Frames.Count - 1);
        Render();
    }

    /// <summary>Tuş eşlemesi: sağ/aşağı ok ve Boşluk sonraki kare, sol/yukarı ok önceki kare, Esc kapatır. true: tuş kullanıldı.</summary>
    public bool HandleKey(Key key)
    {
        switch (key)
        {
            case Key.Right or Key.Space or Key.Down: Move(1); return true;
            case Key.Left or Key.Up: Move(-1); return true;
            case Key.Escape: Close(); return true;
            default: return false;
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e) => e.Handled = HandleKey(e.Key);

    private void Render()
    {
        var frame = _pattern.Frames[_frame];
        var width = _canvas.ActualWidth;
        var height = _canvas.ActualHeight;
        _canvas.Background = Brush(frame.Background);
        _canvas.Children.Clear();

        var label = Luminance(frame.Background) > 128 ? Brushes.Black : new SolidColorBrush(System.Windows.Media.Color.FromRgb(160, 160, 160));
        foreach (var patch in frame.Patches)
        {
            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = Math.Round(patch.Width * width), Height = Math.Round(patch.Height * height),
                Fill = Brush(patch.Color), SnapsToDevicePixels = true,
            };
            Place(rect, patch.X * width, patch.Y * height);
            if (patch.Level is { } level)
            {
                var text = new TextBlock { Text = level.ToString(), Foreground = label, FontSize = 10, Width = rect.Width, TextAlignment = TextAlignment.Center };
                Place(text, patch.X * width, (patch.Y + patch.Height) * height + 4);
            }
        }

        if (frame.Text is { } sample) RenderText(sample, width);
    }

    private void RenderText(PatternText sample, double width)
    {
        var panel = new StackPanel { Width = width - 80 };
        var brush = Brush(sample.Color);
        var line = Loc.Get("SetupWizard.SampleText");
        foreach (var size in sample.Sizes)
        {
            var text = new TextBlock { Text = line, FontSize = size * 96 / 72, Foreground = brush, Margin = new Thickness(0, 6, 0, 6) };
            TextOptions.SetTextFormattingMode(text, TextFormattingMode.Display); // ClearType, gerçek ekran kullanımı
            panel.Children.Add(text);
        }

        Place(panel, 40, 40);
    }

    private void Place(UIElement element, double x, double y)
    {
        Canvas.SetLeft(element, Math.Round(x));
        Canvas.SetTop(element, Math.Round(y));
        _canvas.Children.Add(element);
    }

    private static SolidColorBrush Brush(Rgb color) => new(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));

    private static double Luminance(Rgb c) => 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
}
