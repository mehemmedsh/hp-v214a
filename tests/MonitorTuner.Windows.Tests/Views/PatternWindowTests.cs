using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MonitorTuner.App.Views;
using MonitorTuner.Services;

namespace MonitorTuner.Windows.Tests.Views;

/// <summary>
/// T312 "Desenler açılıyor": sihirbazın her adımındaki her desen <see cref="PatternWindow"/>'da açılır ve her karesi
/// çizilir; ok tuşları kareyi değiştirir (sınırlarda durur), Esc pencereyi kapatır. Pencere gösterilmez: test sırasında
/// gerçek monitörde tam ekran desen yanıp sönmesin.
/// </summary>
public class PatternWindowTests
{
    private const double Width = 1920, Height = 1080;

    private static void RunOnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) throw new InvalidOperationException("STA thread hatası", error);
    }

    /// <summary>Pencereyi göstermeden tuvale ekran boyutu verir (çizim gerçek genişlikle yapılsın).</summary>
    private static Canvas Lay(PatternWindow window)
    {
        var canvas = Assert.IsType<Canvas>(window.Content);
        canvas.Measure(new Size(Width, Height));
        canvas.Arrange(new Rect(0, 0, Width, Height));
        return canvas;
    }

    private static IEnumerable<Pattern> AllPatterns() =>
        Enum.GetValues<WizardStep>().SelectMany(WizardPatterns.For);

    [Fact]
    public void EveryWizardStepPattern_OpensAndDrawsEveryFrame()
    {
        RunOnSta(() =>
        {
            var patterns = AllPatterns().ToList();
            Assert.NotEmpty(patterns);

            foreach (var pattern in patterns)
            {
                var window = new PatternWindow(pattern);
                var canvas = Lay(window);
                Assert.Equal(WindowStyle.None, window.WindowStyle);
                Assert.Equal(WindowState.Maximized, window.WindowState);

                for (var i = 0; i < pattern.Frames.Count; i++)
                {
                    window.Move(i == 0 ? 0 : 1);
                    var frame = pattern.Frames[i];
                    Assert.Equal(i, window.FrameIndex);

                    var background = Assert.IsType<SolidColorBrush>(canvas.Background).Color;
                    Assert.Equal((frame.Background.R, frame.Background.G, frame.Background.B), (background.R, background.G, background.B));

                    var solid = frame.Patches.Where(p => p.StripeColor is null).ToList();
                    var striped = frame.Patches.Where(p => p.StripeColor is not null).ToList();
                    var rects = canvas.Children.OfType<System.Windows.Shapes.Rectangle>().ToList();
                    var images = canvas.Children.OfType<Image>().ToList();
                    Assert.Equal(solid.Count, rects.Count);
                    Assert.Equal(striped.Count, images.Count);
                    for (var p = 0; p < rects.Count; p++)
                    {
                        var patch = solid[p];
                        var fill = Assert.IsType<SolidColorBrush>(rects[p].Fill).Color;
                        Assert.Equal((patch.Color.R, patch.Color.G, patch.Color.B), (fill.R, fill.G, fill.B));
                        Assert.Equal(Math.Round(patch.Width * Width), rects[p].Width); // tam piksel
                        Assert.True(rects[p].Width > 0, $"{pattern.Kind} karesi {i}: yama görünmüyor.");
                    }

                    var labels = frame.Patches.Count(p => p.Level is not null);
                    var text = frame.Text is null ? 0 : 1;
                    Assert.Equal(rects.Count + images.Count + labels + text, canvas.Children.Count);
                }

                window.Close();
            }
        });
    }

    /// <summary>
    /// T315: gamma deseninin çizgili yaması düz renk değil, satır satır (1 fiziksel piksel) siyah ve beyazdır; ölçeklenmeden
    /// (en yakın komşu) çizilir, yoksa çizgiler griye karışır ve ışığın yarısı olmaz.
    /// </summary>
    [Fact]
    public void GammaStripes_AreDrawnPixelByPixel_AlternatingRows()
    {
        RunOnSta(() =>
        {
            var pattern = WizardPatterns.For(WizardStep.Gamma).Single();
            var window = new PatternWindow(pattern);
            var canvas = Lay(window);
            var patch = pattern.Frames[0].Patches.Single(p => p.StripeColor is not null);

            var image = Assert.Single(canvas.Children.OfType<Image>());
            var bitmap = Assert.IsAssignableFrom<BitmapSource>(image.Source);
            var dpi = VisualTreeHelper.GetDpi(window); // CI'daki Windows'un ölçeklemesi %100 olmayabilir
            Assert.Equal((int)Math.Round(patch.Width * Width * dpi.DpiScaleX), bitmap.PixelWidth);
            Assert.Equal((int)Math.Round(patch.Height * Height * dpi.DpiScaleY), bitmap.PixelHeight);
            Assert.Equal(bitmap.PixelWidth, image.Width * dpi.DpiScaleX, 6); // bir bit eşlem pikseli = bir ekran pikseli
            Assert.Equal(bitmap.PixelHeight, image.Height * dpi.DpiScaleY, 6);
            Assert.Equal(Stretch.None, image.Stretch);
            Assert.Equal(BitmapScalingMode.NearestNeighbor, RenderOptions.GetBitmapScalingMode(image));

            var stride = bitmap.PixelWidth * 4;
            var pixels = new byte[stride * 4];
            bitmap.CopyPixels(new Int32Rect(0, 0, bitmap.PixelWidth, 4), pixels, stride, 0);
            for (var row = 0; row < 4; row++)
            {
                var expected = row % 2 == 0 ? patch.Color : patch.StripeColor!.Value;
                foreach (var x in new[] { 0, bitmap.PixelWidth / 2, bitmap.PixelWidth - 1 })
                {
                    var i = row * stride + x * 4; // Bgr32
                    Assert.Equal((expected.B, expected.G, expected.R), (pixels[i], pixels[i + 1], pixels[i + 2]));
                }
            }
        });
    }

    [Fact]
    public void ArrowKeys_MoveBetweenFrames_AndStopAtBothEnds()
    {
        RunOnSta(() =>
        {
            var pattern = WizardPatterns.For(WizardStep.DeadPixelsAndUniformity).First(p => p.Kind == PatternKind.DeadPixels);
            var last = pattern.Frames.Count - 1;
            Assert.True(last >= 2);
            var window = new PatternWindow(pattern);
            var canvas = Lay(window);

            Assert.Equal(0, window.FrameIndex);
            Assert.True(window.HandleKey(Key.Left)); // ilk karede geri gidilmez
            Assert.Equal(0, window.FrameIndex);

            Assert.True(window.HandleKey(Key.Right));
            Assert.Equal(1, window.FrameIndex);
            var shown = Assert.IsType<SolidColorBrush>(canvas.Background).Color;
            Assert.Equal(pattern.Frames[1].Background.R, shown.R); // çizilen kare değişti
            Assert.True(window.HandleKey(Key.Space));
            Assert.True(window.HandleKey(Key.Down));
            Assert.Equal(3, window.FrameIndex);
            Assert.True(window.HandleKey(Key.Up));
            Assert.Equal(2, window.FrameIndex);

            for (var i = 0; i < 10; i++) window.HandleKey(Key.Right); // son karede durur
            Assert.Equal(last, window.FrameIndex);

            window.Move(-100);
            Assert.Equal(0, window.FrameIndex);
            window.Move(100);
            Assert.Equal(last, window.FrameIndex);

            Assert.False(window.HandleKey(Key.A)); // başka tuşlar kareyi değiştirmez
            Assert.Equal(last, window.FrameIndex);
        });
    }

    [Fact]
    public void Escape_ClosesTheWindow()
    {
        RunOnSta(() =>
        {
            var window = new PatternWindow(WizardPatterns.For(WizardStep.WhiteMatch)[0]);
            var closed = false;
            window.Closed += (_, _) => closed = true;

            Assert.True(window.HandleKey(Key.Escape));

            Assert.True(closed);
        });
    }

    [Fact]
    public void EmptyPattern_IsRefused() =>
        RunOnSta(() => Assert.Throws<ArgumentException>(() => new PatternWindow(new Pattern(PatternKind.WhiteField, []))));
}
