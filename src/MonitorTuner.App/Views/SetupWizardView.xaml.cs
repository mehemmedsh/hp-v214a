using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MonitorTuner.App.ViewModels;
using MonitorTuner.Services;

namespace MonitorTuner.App.Views;

/// <summary>
/// Ayar sihirbazı sayfası (T315). DataContext: <see cref="SetupWizardViewModel"/>. Mantık yok: düğmeler ViewModel'i çağırır,
/// desen <see cref="PatternWindow"/>'da bu pencerenin ekranında tam ekran ve modal açılır. Isınma sayacı yarım dakikada bir yenilenir
/// (sadece metin; hiçbir şey yazılmaz).
/// </summary>
public partial class SetupWizardView : UserControl
{
    private readonly DispatcherTimer _warmUpTimer;

    public SetupWizardView()
    {
        InitializeComponent();
        _warmUpTimer = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, (_, _) => ViewModel?.RefreshWarmUp(), Dispatcher);
        _warmUpTimer.Stop();
        Loaded += (_, _) => _warmUpTimer.Start();
        Unloaded += (_, _) => _warmUpTimer.Stop();
        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is SetupWizardViewModel vm) vm.ShowPattern = ShowPattern;
        };
    }

    private SetupWizardViewModel? ViewModel => DataContext as SetupWizardViewModel;

    /// <summary>Deseni ana pencerenin ekranında açar ve kapanana kadar bekler; son gösterilen kareyi döner.</summary>
    private int ShowPattern(Pattern pattern)
    {
        var window = new PatternWindow(pattern);
        if (Window.GetWindow(this) is { IsVisible: true } owner)
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner; // ekranlı kurulumda sahibin ekranında büyür
        }

        window.ShowDialog();
        return window.FrameIndex;
    }

    private void Start_Click(object sender, RoutedEventArgs e) => ViewModel?.Start().Forget();

    private void Back_Click(object sender, RoutedEventArgs e) => ViewModel?.Back().Forget();

    private void Next_Click(object sender, RoutedEventArgs e) => ViewModel?.Next().Forget();

    private void Finish_Click(object sender, RoutedEventArgs e) => ViewModel?.Finish().Forget();

    private void Cancel_Click(object sender, RoutedEventArgs e) => ViewModel?.Cancel().Forget();

    private void OpenPattern_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PatternItem item }) ViewModel?.OpenPattern(item);
    }

    /// <summary>Sürükleme bitti, tuş bırakıldı ya da tıklandı: tek yazma (aynı değer ikinci kez yazılmaz).</summary>
    private void Ddc_Released(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PanelItemViewModel item }) ViewModel?.CommitDdc(item).Forget();
    }

    private void Gain_Released(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GainSliderViewModel item }) ViewModel?.CommitGain(item).Forget();
    }
}
