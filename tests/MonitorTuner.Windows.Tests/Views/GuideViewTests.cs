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

    /// <summary>T322: ana pencere sayfaları bir kez kurar; rehber sayfası gezinmeyle görünür, başta görüntü modları açık.</summary>
    [Fact]
    public void MainWindow_ContainsGuideView_AndOpensOnTheModesPage()
    {
        RunOnSta(() =>
        {
            var window = new MainWindow();

            Assert.IsType<GuideView>(window.FindName("GuideView"));
            Assert.Equal(System.Windows.Visibility.Visible, ((System.Windows.UIElement)window.FindName("ModesPage")).Visibility);
            Assert.Equal(System.Windows.Visibility.Collapsed, ((System.Windows.UIElement)window.FindName("GuideView")).Visibility);

            var nav = (ListBox)window.FindName("Nav");
            nav.SelectedItem = window.FindName("NavGuide");

            Assert.Equal(System.Windows.Visibility.Visible, ((System.Windows.UIElement)window.FindName("GuideView")).Visibility);
            Assert.Equal(System.Windows.Visibility.Collapsed, ((System.Windows.UIElement)window.FindName("ModesPage")).Visibility);
        });
    }
}
