using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using MonitorTuner.App.Localization;
using MonitorTuner.App.ViewModels;
using MonitorTuner.App.Views;
using MonitorTuner.Composition;
using MonitorTuner.Core.Profiles;
using MonitorTuner.Services;

namespace MonitorTuner.App;

/// <summary>
/// Uygulama açılışı ve kapanışı (T314). Mantık yok: bağlama <see cref="AppComposition"/> ve <see cref="AppRuntime"/>'da. Burada sadece
/// sıra var: servisleri kur, <see cref="AppRuntime.Start"/>, tepsiyi bağla, pencereyi göster, zamanlayıcıyı çalıştır; kapanışta önce
/// <see cref="AppRuntime.Shutdown"/> (efektler kalkar, sadece kalibrasyon kalır), sonra tepsi simgesi ve kısayol bırakılır.
/// Desteklenen monitör yoksa ya da profil okunamazsa hiçbir şeye yazılmadan söylenip kapanır. Aynı anda tek örnek çalışır
/// (iki örnek aynı GPU tablosunu ve DDC'yi birbirinden habersiz yazardı).
/// </summary>
public partial class App : System.Windows.Application
{
    /// <summary>Başka program GPU tablosunu değiştirdi mi bakma sıklığı; tablo okumak ucuzdur, yazma sadece değişince olur.</summary>
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);

    private static readonly string SettingsRoot = JsonSettingsStore.DefaultRoot();

    /// <summary>Açılış adımları ve beklenmeyen hatalar (T314 düzeltme): sessiz kapanmanın sebebi buradan okunur.</summary>
    private static readonly AppLog Log = new(AppLog.DefaultFolder(SettingsRoot), TimeProvider.System);

    /// <summary>Servis çağrılarının tek sırası (T316): yavaş DDC işi arka planda, tepsi ve zamanlayıcı da aynı sıradan geçer.</summary>
    private readonly WorkLane _lane = new();

    private Mutex? _singleInstance;
    private AppRuntime? _runtime;
    private TrayViewModel? _trayViewModel;
    private SetupWizardViewModel? _wizardViewModel;
    private DdcPanelViewModel? _ddcPanelViewModel;
    private ModesViewModel? _modesViewModel;
    private ShellViewModel? _shellViewModel;
    private TrayHost? _tray;
    private DispatcherTimer? _timer;
    private MainWindow? _main;
    private bool _exiting;
    private bool _tickRunning;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Log.Write($"açılış: sürüm {typeof(App).Assembly.GetName().Version}, Windows {Environment.OSVersion.Version}, klasör {AppContext.BaseDirectory}");
        DispatcherUnhandledException += OnUnhandledUiException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error("arka plan", args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()));

        // Arayüz dili (T502): seçilmedikçe Türkçe, Windows'un dilinden bağımsız. İlk iletiden önce ayarlanır.
        var language = CultureInfo.GetCultureInfo(LanguagePreference.Load(SettingsRoot));
        CultureInfo.DefaultThreadCurrentUICulture = language;
        CultureInfo.CurrentUICulture = language;
        Log.Write("dil: " + language.Name);

        _singleInstance = new Mutex(initiallyOwned: true, @"Local\MonitorTuner.SingleInstance", out var first);
        if (!first)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            Log.Write("ikinci örnek: uygulama zaten çalışıyor, çıkılıyor");
            System.Windows.MessageBox.Show(StartupNotices.AlreadyRunning(), Loc.Get("Common.AppTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0); // zaten çalışıyor: tepsideki örnek kullanılır
            return;
        }

        AppLaunch launch;
        try
        {
            launch = AppComposition.Create();
        }
        catch (InvalidProfileException ex)
        {
            Log.Error("monitör profili", ex);
            Fail(StartupNotices.ProfileError(ex.Message));
            return;
        }
        catch (Exception ex)
        {
            Log.Error("bağlama", ex);
            Fail(StartupNotices.StartFailed(ex.Message));
            return;
        }

        Log.Write("görülen ekranlar: " + string.Join("; ", launch.Monitors.Select(m => $"{m.Model} {m.Connection} {m.Display.Value}")));

        if (launch.Runtime is not { } runtime)
        {
            Log.Write("desteklenen monitör yok");
            Fail(StartupNotices.NoSupportedMonitor(launch.Monitors));
            return;
        }

        _runtime = runtime;
        StartupReport report;
        try
        {
            report = runtime.Start();
            Log.Write($"Start: {report}");
        }
        catch (Exception ex)
        {
            Log.Error("Start", ex);
            Fail(StartupNotices.StartFailed(ex.Message));
            return;
        }

        try
        {
            _trayViewModel = new TrayViewModel(new LanedQuickControls(runtime.QuickControls, _lane), TrayHost.HotKeyText)
            {
                OpenMainWindow = OpenMainWindow,
                Exit = ExitApplication,
            };
            _tray = new TrayHost(_trayViewModel);
            Log.Write("tepsi hazır");
            _wizardViewModel = new SetupWizardViewModel(runtime.Wizard, runtime.DdcPanel, _lane); // T315: pencere kapansa da sihirbaz durumu kalır
            _ddcPanelViewModel = new DdcPanelViewModel(runtime.DdcPanel, _lane);
            _modesViewModel = new ModesViewModel(runtime.QuickControls, _lane); // T319: ana sekmede görüntü modları
            // T322: kenar çubuğunda monitör, bağlantı, şu anki Hz (Windows'tan, sırada okunur) ve sürüm
            _shellViewModel = new ShellViewModel(runtime.Monitor.Model, runtime.Monitor.Connection,
                () => runtime.RefreshRates.CurrentRefreshHz, AppVersion(), _lane);
            if (StartupNotices.From(report) is { Count: > 0 } notices) _trayViewModel.Announce(string.Join(Environment.NewLine, notices));

            OpenMainWindow();
            Log.Write("ana pencere açıldı");
        }
        catch (Exception ex)
        {
            Log.Error("tepsi ve pencere", ex);
            Fail(StartupNotices.Unexpected(ex.Message, Log.FilePath));
            return;
        }

        _timer = new DispatcherTimer(TickInterval, DispatcherPriority.Background, OnTick, Dispatcher);
        _timer.Start();
    }

    /// <summary>
    /// Arayüzde yakalanmayan hata: günlüğe yazılır, kullanıcıya söylenir ve uygulama güvenli kapanır (efektler kalkar, kalibrasyon
    /// kalır). Bilinmeyen bir durumda çalışmaya devam etmek yerine kullanıcının bilinen ayarına dönülür.
    /// </summary>
    private void OnUnhandledUiException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Log.Error("arayüz", e.Exception);
        if (_exiting) return;
        Fail(StartupNotices.Unexpected(e.Exception.Message, Log.FilePath));
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        Cleanup(); // oturum kapanırken efektler kalkar
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Cleanup();
        base.OnExit(e);
    }

    /// <summary>Arka planda (T316); önceki tur bitmeden yenisi başlamaz (DDC uzun sürerse turlar birikmez).</summary>
    private async void OnTick(object? sender, EventArgs e)
    {
        if (_runtime is not { } runtime || _exiting || _tickRunning) return;
        _tickRunning = true;
        try
        {
            var report = await _lane.RunAsync(runtime.Tick);
            if (report.ExternalTableRestored && !_exiting) _trayViewModel?.Announce(StartupNotices.ExternalTableRestored());
        }
        finally
        {
            _tickRunning = false;
        }
    }

    /// <summary>Kenar çubuğunda ve Ayarlar → Hakkında'da görünen sürüm (derlemenin sürümü, üç basamak).</summary>
    private static string AppVersion() => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "?";

    private void OpenMainWindow()
    {
        if (_main is null)
        {
            _main = _wizardViewModel is not null && _ddcPanelViewModel is not null && _modesViewModel is not null && _shellViewModel is not null
                ? new MainWindow(_wizardViewModel, _ddcPanelViewModel, _modesViewModel, _shellViewModel, SettingsRoot)
                : new MainWindow();
            _main.Closed += (_, _) => _main = null; // pencere kapanınca uygulama tepside çalışmaya devam eder
        }
        _main.Show();
        if (_main.WindowState == WindowState.Minimized) _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    private void ExitApplication()
    {
        Cleanup();
        Shutdown();
    }

    /// <summary>Sıra: zamanlayıcı durur, servislerin Shutdown'u, sonra tepsi simgesi ve kısayol. Birden çok kez çağrılabilir.</summary>
    private void Cleanup()
    {
        if (_exiting) return;
        _exiting = true;
        Log.Write("kapanış");
        _timer?.Stop();
        if (_runtime is { } runtime) _lane.Run(runtime.Shutdown); // arka planda süren DDC işi bitince; ikisi aynı anda çalışmaz
        _tray?.Dispose();
        _tray = null;
        try
        {
            _singleInstance?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Mutex bu iş parçacığına ait değil (oturum kapanışı); süreç bitince Windows bırakır.
        }
        _singleInstance?.Dispose();
        _singleInstance = null;
    }

    /// <summary>Önce güvenli kapanış (efektler kalkar), sonra ileti. Kapanışın kendi hatası da günlüğe yazılır, yutulmaz ama uygulamayı düşürmez.</summary>
    private void Fail(string message)
    {
        Log.Write("çıkış iletisi: " + message.ReplaceLineEndings(" "));
        try
        {
            Cleanup();
        }
        catch (Exception ex)
        {
            Log.Error("kapanış", ex);
        }

        System.Windows.MessageBox.Show(message, Loc.Get("Common.AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        Shutdown(1);
    }
}
