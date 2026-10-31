using System.Threading;
using System.Windows.Controls;
using MonitorTuner.App;
using MonitorTuner.App.Localization;
using MonitorTuner.App.Views;

namespace MonitorTuner.Windows.Tests.Views;

/// <summary>
/// T311: Rehber arayüzünün (GuideView) ve MainWindow rehber sekmesinin başlatılmasını doğrular.
/// </summary>
public class GuideViewTests
{
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

        if (error is not null)
        {
            throw new InvalidOperationException("STA thread hatası", error);
        }
    }

    [Fact]
    public void Loc_ReturnsGuideTexts_WithoutMissingMarker()
    {
        var title = Loc.Get("Guide.TabTitle");
        Assert.False(title.StartsWith("[[", StringComparison.Ordinal));
        Assert.False(title.EndsWith("]]", StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(title));
    }

    [Fact]
    public void GuideView_CanBeInstantiated_OnStaThread()
    {
        RunOnSta(() =>
        {
            var view = new GuideView();
            Assert.NotNull(view);
            Assert.IsAssignableFrom<UserControl>(view);
        });
    }

    [Fact]
    public void MainWindow_ContainsGuideTabOrView()
    {
        RunOnSta(() =>
        {
            var window = new MainWindow();
            Assert.NotNull(window);

            // MainWindow içinde rehber bileşeni ya da sekmesi yer almalı
            var hasGuideView = false;
            if (window.Content is TabControl tabControl)
            {
                foreach (var item in tabControl.Items)
                {
                    if (item is TabItem tabItem && (tabItem.Content is GuideView || tabItem.Header?.ToString()?.Contains("Rehber") == true))
                    {
                        hasGuideView = true;
                        break;
                    }
                }
            }
            else if (window.Content is GuideView)
            {
                hasGuideView = true;
            }

            Assert.True(hasGuideView, "MainWindow içinde GuideView veya Rehber sekmesi bulunmalıdır.");
        });
    }
}
