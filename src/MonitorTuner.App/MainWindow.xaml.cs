using System.Windows;
using System.Windows.Data;
using MonitorTuner.App.ViewModels;

namespace MonitorTuner.App;

/// <summary>
/// Ana pencere. ViewModel'ler uygulama boyunca yaşar (App verir): pencere kapanıp açılınca açık sihirbaz kaldığı adımda görünür.
/// Parametresiz kurucu tasarım ve testler içindir; sekmeler o zaman boş kalır.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    public MainWindow(SetupWizardViewModel wizard, DdcPanelViewModel ddcPanel) : this()
    {
        WizardView.DataContext = wizard;
        DdcView.DataContext = ddcPanel;
        DdcTab.SetBinding(IsEnabledProperty, new Binding(nameof(SetupWizardViewModel.IsIdle)) { Source = wizard });
    }
}
