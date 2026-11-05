using System.Windows;
using System.Windows.Controls;
using MonitorTuner.App.ViewModels;

namespace MonitorTuner.App.Views;

/// <summary>
/// DDC paneli (T305). DataContext: <see cref="DdcPanelViewModel"/> (bağlama kökü verir; kök yazılana kadar boş kalır).
/// Kaydırıcı bırakılınca ViewModel tek yazma yapar.
/// </summary>
public partial class DdcPanelView : UserControl
{
    public DdcPanelView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is DdcPanelViewModel vm)
            {
                vm.ConfirmInputSwitch ??= (title, text) =>
                    MessageBox.Show(text, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
                vm.Refresh().Forget(); // pencere beklemeden açılır, panel arka planda dolar (T316)
            }
        };
    }

    private DdcPanelViewModel? ViewModel => DataContext as DdcPanelViewModel;

    private void Refresh_Click(object sender, RoutedEventArgs e) => ViewModel?.Refresh().Forget();

    /// <summary>Sürükleme bitti, tuş bırakıldı ya da fareyle tıklandı. Aynı değer ikinci kez gelirse ViewModel yazmaz.</summary>
    private void Slider_Released(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PanelItemViewModel item }) ViewModel?.Commit(item).Forget();
    }
}
