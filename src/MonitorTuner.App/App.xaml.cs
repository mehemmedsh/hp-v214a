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

    private Mutex? _singleInstance;
    private AppRuntime? _runtime;
    private TrayViewModel? _trayViewModel;
    private SetupWizardViewModel? _wizardViewModel;
    private DdcPanelViewModel? _ddcPanelViewModel;
    private TrayHost? _tray;
    private DispatcherTimer? _timer;
    private MainWindow? _main;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _singleInstance = new Mutex(initiallyOwned: true, @"Local\MonitorTuner.SingleInstance", out var first);
        if (!first)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
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
            Fail(StartupNotices.ProfileError(ex.Message));
            return;
        }
        catch (Exception ex)
        {
            Fail(StartupNotices.StartFailed(ex.Message));
            return;
        }

        if (launch.Runtime is not { } runtime)
        {
            Fail(StartupNotices.NoSupportedMonitor(launch.Monitors));
            return;
        }

        _runtime = runtime;
        StartupReport report;
        try
        {
            report = runtime.Start();
        }
        catch (Exception ex)
        {
            Fail(StartupNotices.StartFailed(ex.Message));
            return;
        }

        _trayViewModel = new TrayViewModel(runtime.QuickControls, TrayHost.HotKeyText)
        {
            OpenMainWindow = OpenMainWindow,
            Exit = ExitApplication,
        };
        _tray = new TrayHost(_trayViewModel);
        _wizardViewModel = new SetupWizardViewModel(runtime.Wizard, runtime.DdcPanel); // T315: pencere kapansa da sihirbaz durumu kalır
        _ddcPanelViewModel = new DdcPanelViewModel(runtime.DdcPanel);
        if (StartupNotices.From(report) is { Count: > 0 } notices) _trayViewModel.Announce(string.Join(Environment.NewLine, notices));

        OpenMainWindow();
        _timer = new DispatcherTimer(TickInterval, DispatcherPriority.Background, OnTick, Dispatcher);
        _timer.Start();
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

    private void OnTick(object? sender, EventArgs e)
    {
        if (_runtime is null || _exiting) return;
        if (_runtime.Tick().ExternalTableRestored) _trayViewModel?.Announce(StartupNotices.ExternalTableRestored());
    }

    private void OpenMainWindow()
    {
        if (_main is null)
        {
            _main = _wizardViewModel is not null && _ddcPanelViewModel is not null
                ? new MainWindow(_wizardViewModel, _ddcPanelViewModel)
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
        _timer?.Stop();
        _runtime?.Shutdown();
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

    private void Fail(string message)
    {
        System.Windows.MessageBox.Show(message, Loc.Get("Common.AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        Cleanup();
        Shutdown(1);
    }
}
