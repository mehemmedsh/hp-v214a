using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MonitorTuner.App.Localization;
using MonitorTuner.App.ViewModels;
using MonitorTuner.Services;

namespace MonitorTuner.App;

/// <summary>
/// Ana pencere. ViewModel'ler uygulama boyunca yaşar (App verir): pencere kapanıp açılınca açık sihirbaz kaldığı adımda görünür.
/// Parametresiz kurucu tasarım ve testler içindir; sekmeler ve dil seçimi o zaman boş kalır.
/// </summary>
public partial class MainWindow : Window
{
    private sealed record LanguageItem(string Code, string Title);

    private string? _settingsRoot;
    private bool _loadingLanguage;

    public MainWindow() => InitializeComponent();

    /// <param name="settingsRoot">Dil seçiminin saklandığı ayar kökü (<see cref="LanguagePreference"/>).</param>
    public MainWindow(SetupWizardViewModel wizard, DdcPanelViewModel ddcPanel, string settingsRoot) : this()
    {
        WizardView.DataContext = wizard;
        DdcView.DataContext = ddcPanel;
        DdcTab.SetBinding(IsEnabledProperty, new Binding(nameof(SetupWizardViewModel.IsIdle)) { Source = wizard });

        _settingsRoot = settingsRoot;
        _loadingLanguage = true;
        var items = LanguagePreference.Supported.Select(code => new LanguageItem(code, Loc.Get("Common.Language_" + code))).ToList();
        LanguageBox.ItemsSource = items;
        LanguageBox.SelectedItem = items.Single(i => i.Code == LanguagePreference.Load(settingsRoot));
        _loadingLanguage = false;
    }

    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingLanguage || _settingsRoot is null || LanguageBox.SelectedItem is not LanguageItem item) return;
        try
        {
            LanguagePreference.Save(_settingsRoot, item.Code);
            LanguageNote.Text = Loc.Get("Common.Language_Saved");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LanguageNote.Text = string.Format(CultureInfo.CurrentCulture, Loc.Get("Common.Language_SaveFailed"), ex.Message);
        }
    }
}
