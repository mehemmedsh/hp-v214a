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
/// Ana pencere (T322): solda gezinme (görüntü modları, monitör ayarları, ayar sihirbazı, rehber, ayarlar), sağda sayfalar. Sayfalar
/// bir kez kurulur, gezinme sadece görünürlüğü değiştirir. ViewModel'ler uygulama boyunca yaşar (App verir): pencere kapanıp açılınca
/// açık sihirbaz kaldığı adımda görünür. Parametresiz kurucu tasarım ve testler içindir; sayfalar ve dil seçimi o zaman boş kalır.
/// </summary>
public partial class MainWindow : Window
{
    private sealed record LanguageItem(string Code, string Title);

    private string? _settingsRoot;
    private bool _loadingLanguage;
    private ModesViewModel? _modes;
    private ShellViewModel? _shell;
    private QuickSettingsViewModel? _quick;

    public MainWindow()
    {
        InitializeComponent();
        Nav.SelectedIndex = 0; // XAML'de değil: seçim olayı sayfalar kurulmadan gelmesin
    }

    /// <param name="modes">Görüntü modları (T319, T322); pencere her açıldığında ve öne geldiğinde liste ve etkin mod yenilenir.</param>
    /// <param name="quick">Modlar sayfasındaki parlaklık, mavi ışık ve panik (T323).</param>
    /// <param name="shell">Kenar çubuğundaki monitör, Hz ve sürüm bilgisi (T322).</param>
    /// <param name="breaks">Ayarlar sayfasındaki mola hatırlatıcısı anahtarı (T324).</param>
    /// <param name="settingsRoot">Dil seçiminin saklandığı ayar kökü (<see cref="LanguagePreference"/>).</param>
    public MainWindow(
        SetupWizardViewModel wizard, DdcPanelViewModel ddcPanel, ModesViewModel modes, QuickSettingsViewModel quick, ShellViewModel shell,
        BreakReminderViewModel breaks, string settingsRoot) : this()
    {
        BreakCard.DataContext = breaks;
        WizardView.DataContext = wizard;
        DdcView.DataContext = ddcPanel;
        NavMonitor.SetBinding(IsEnabledProperty, new Binding(nameof(SetupWizardViewModel.IsIdle)) { Source = wizard });

        _modes = modes;
        modes.Reload();
        ModesPanel.DataContext = modes;
        ModesPanel.SetBinding(IsEnabledProperty, new Binding(nameof(SetupWizardViewModel.IsIdle)) { Source = wizard });

        // Hızlı ayarlar (T323): mod seçince kaydırıcılar yeni değerleri okur, elle değişince ekrandaki mod "elle değişti" olur.
        _quick = quick;
        QuickPanel.DataContext = quick;
        quick.Changed += modes.RefreshActive;
        modes.Applied += () => quick.Load().Forget();
        quick.Load().Forget();

        _shell = shell;
        ShellPanel.DataContext = shell;
        SettingsPage.DataContext = shell;
        shell.Refresh().Forget();

        _settingsRoot = settingsRoot;
        DataFolderText.Text = string.Format(CultureInfo.CurrentCulture, Loc.Get("Shell.Settings_Data"), settingsRoot);
        _loadingLanguage = true;
        var items = LanguagePreference.Supported.Select(code => new LanguageItem(code, Loc.Get("Common.Language_" + code))).ToList();
        LanguageBox.ItemsSource = items;
        LanguageBox.SelectedItem = items.Single(i => i.Code == LanguagePreference.Load(settingsRoot));
        _loadingLanguage = false;

        // Tepsiden mod seçilip pencereye dönülünce etkin mod ve Hz güncel görünsün; hiçbir şey yazılmaz.
        Activated += (_, _) =>
        {
            _modes?.RefreshActive(); // sadece etkin mod değiştiyse kartları yeniden kurar (ilk tıklama kaybolmasın)
            if (_quick is { IsBusy: false }) _quick.Load().Forget(); // tepsiden değişmiş olabilir; sadece okur
            _shell?.Refresh().Forget();
        };
    }

    /// <summary>Gezinme: seçilen sayfa görünür, diğerleri gizlenir (sayfalar ve durumları yerinde kalır).</summary>
    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Nav.SelectedItem is not ListBoxItem { Tag: string page }) return;
        Show(ModesPage, page == "Modes");
        Show(DdcView, page == "Monitor");
        Show(WizardView, page == "Wizard");
        Show(GuideView, page == "Guide");
        Show(SettingsPage, page == "Settings");
    }

    private static void Show(UIElement? element, bool visible)
    {
        if (element is not null) element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Moda tıklandı (T319): tek uygulama, arka planda; sonuç ve etkin mod kartların üstünde yazar.</summary>
    private void Mode_Click(object sender, RoutedEventArgs e)
    {
        if (_modes is null || (sender as FrameworkElement)?.DataContext is not ModeItem mode) return;
        _modes.Apply(mode).Forget();
    }

    /// <summary>Parlaklık kaydırıcısı bırakıldı (sürükleme bitti, tuş ya da tıklama): değer değiştiyse tek yazma.</summary>
    private void Brightness_Released(object sender, RoutedEventArgs e) => _quick?.CommitBrightness().Forget();

    private void Kelvin_Released(object sender, RoutedEventArgs e) => _quick?.CommitKelvin().Forget();

    private void Panic_Click(object sender, RoutedEventArgs e) => _quick?.Panic().Forget();

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
