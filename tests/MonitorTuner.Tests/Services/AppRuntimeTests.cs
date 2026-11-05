using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Core.Profiles;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;
using MonitorTuner.Services;
using MonitorTuner.Tests.Fakes;

namespace MonitorTuner.Tests.Services;

/// <summary>
/// Bağlama (T314): <see cref="AppRuntime"/> gerçek donanımın yerine sahte donanımla kurulur; her servis doğru bağlanmış mı,
/// açılış ve kapanış sırası doğru mu, desteklenmeyen monitörde hiçbir şey yazılmıyor mu. Gerçek donanım (Windows) T314'ün
/// ince <c>AppComposition</c> sınıfındadır ve burada test edilmez.
/// </summary>
public sealed class AppRuntimeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mt-runtime-" + Guid.NewGuid().ToString("N"));
    private readonly FakeProbe _probe = new();

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class FakeProbe : IColorConflictProbe
    {
        public List<ColorConflict> Found { get; } = [];
        public IReadOnlyList<ColorConflict> Detect() => [.. Found];
    }

    private static MonitorProfile Profile() =>
        new MonitorProfileReader().Read(File.ReadAllText(Path.Combine(RepoRoot.Find(), "profiles", "hp-p21b-g4.json")));

    private AppLaunch Launch(FakeHardware hardware) =>
        AppRuntime.Create(
            new AppPorts(hardware.Displays, hardware.Monitor, hardware.Gpu, hardware.Modes, hardware.Nvidia, hardware.GammaRange, _probe),
            [Profile()], new JsonSettingsStore(_root, hardware.Clock), hardware.Clock);

    private AppRuntime Runtime(FakeHardware hardware) =>
        Launch(hardware).Runtime ?? throw new InvalidOperationException("Destekli monitör bulunamadı.");

    private static void AssertNothingWritten(FakeHardware hardware)
    {
        Assert.Empty(hardware.Monitor.Writes);
        Assert.Empty(hardware.Gpu.Writes);
        Assert.Equal(0, hardware.GammaRange.WriteCount);
        Assert.Equal(0, hardware.Nvidia.WriteCount);
        Assert.Empty(hardware.Modes.Switches);
        Assert.Equal(0, hardware.Modes.PersistCount);
    }

    private static CalibrationChoices SomeCalibration() =>
        new(2.4, new RgbGains(1.1, 1.0, 0.9), new RgbGains(1.0, 1.0, 1.0), new RgbGains(0.9, 1.0, 1.1));

    private static bool IsIdentity(GammaRamp? table) => table is not null && table.Equals(GammaRamp.Identity);

    // --- Monitör seçimi ---

    [Fact]
    public void Create_PicksTheMonitorTheProfileMatches_AndWritesNothing()
    {
        var hardware = new FakeHardware(75, 76);

        var launch = Launch(hardware);

        var runtime = Assert.IsType<AppRuntime>(launch.Runtime);
        Assert.Equal("HP P21b G4", runtime.Monitor.Model);
        Assert.Equal(FakeHardware.Display, runtime.Monitor.Display);
        Assert.Equal("HP P21b G4", runtime.Profile.Model);
        Assert.Single(launch.Monitors);
        AssertNothingWritten(hardware); // kurmak hiçbir şeye yazmaz; yazma Start ve kullanıcı eylemleriyle
    }

    [Fact]
    public void Create_SkipsUnreadableDisplays_AndUsesTheSupportedOne()
    {
        var hardware = new FakeHardware();
        hardware.Displays.Connected.Insert(0,
            new ConnectedDisplay(new DisplayId(@"\\.\DISPLAY2"), new byte[128], ConnectionType.DisplayPort, "DP", "NVIDIA GeForce RTX 3090"));

        var launch = Launch(hardware);

        Assert.Equal(2, launch.Monitors.Count);
        Assert.Equal(FakeHardware.Display, Assert.IsType<AppRuntime>(launch.Runtime).Monitor.Display);
    }

    [Fact]
    public void Create_ReturnsNoRuntime_WhenNoDisplayMatchesAProfile_AndWritesNothing()
    {
        var hardware = new FakeHardware();
        hardware.Displays.Connected.Clear();
        hardware.Displays.Connected.Add(
            new ConnectedDisplay(FakeHardware.Display, new byte[128], ConnectionType.Hdmi, "HDMI", "NVIDIA GeForce RTX 3080"));

        var launch = Launch(hardware);

        Assert.Null(launch.Runtime);
        Assert.Single(launch.Monitors); // arayüz "şu ekranları gördüm" diyebilsin
        AssertNothingWritten(hardware);
    }

    [Fact]
    public void Create_ReturnsNoRuntime_WhenThereAreNoDisplays()
    {
        var hardware = new FakeHardware();
        hardware.Displays.Connected.Clear();

        var launch = Launch(hardware);

        Assert.Null(launch.Runtime);
        Assert.Empty(launch.Monitors);
    }

    // --- Bağlama ---

    [Fact]
    public void Create_WiresEveryServiceToTheSameMonitor()
    {
        var hardware = new FakeHardware(75, 76);
        var runtime = Runtime(hardware);

        Assert.True(runtime.DdcPanel.Refresh().Available);                         // DDC → panel
        Assert.Contains(76, runtime.RefreshRates.GetAvailableRatesHz());           // ekran modları → Hz paneli
        Assert.Equal(60, runtime.RefreshRates.CurrentRefreshHz);
        Assert.Contains(runtime.Profiles.Profiles, p => p.Name == "Gündüz" || p.BuiltIn);
        Assert.Equal(runtime.Profiles.Profiles.Count, runtime.QuickControls.Profiles.Count);
        Assert.True(runtime.QuickControls.Enabled);                                 // sihirbaz kapalı
        Assert.False(runtime.Wizard.IsActive);
        Assert.Null(runtime.Wizard.LoadBaseline());
    }

    [Fact]
    public void Create_UsesTheSettingsStoreBackupFolder_ForTheFactoryBackup()
    {
        var hardware = new FakeHardware();
        var runtime = Runtime(hardware);

        runtime.Start();
        runtime.Brightness.Commit(0.5); // ilk DDC yazması da ilk açılış yedeğini ister

        // Kurtarma dosyası (T104) yedeği tam bu klasörde arar: JsonFactoryBackupStore.DefaultFolder() = <kök>\backups.
        var backup = new JsonFactoryBackupStore(Path.Combine(_root, "backups")).Load();
        Assert.NotNull(backup);
        Assert.Equal(Path.Combine(JsonSettingsStore.DefaultRoot(), "backups"), JsonFactoryBackupStore.DefaultFolder());
    }

    [Fact]
    public void Create_LetsTheConflictProbeReachTheBlueLightFilter()
    {
        var hardware = new FakeHardware();
        _probe.Found.Add(ColorConflict.Flux);
        var runtime = Runtime(hardware);
        runtime.Start();

        var state = runtime.BlueLight.SetKelvin(3400);

        Assert.Contains(ColorConflict.Flux, state.Conflicts);
    }

    [Fact]
    public void Panic_TurnsEffectsOff_ThroughTheQuickControls()
    {
        var hardware = new FakeHardware();
        var runtime = Runtime(hardware);
        runtime.Start();
        runtime.Brightness.Commit(0.05); // ek karartma
        Assert.True(runtime.Brightness.DimmingActive);

        var result = runtime.QuickControls.Panic();

        Assert.Equal(QuickOutcome.Done, result.Outcome);
        Assert.False(runtime.Brightness.DimmingActive);
        Assert.True(IsIdentity(hardware.Gpu.Read(FakeHardware.Display)));
    }

    // --- Açılış ---

    [Fact]
    public void Start_WithNothingSavedAndACleanTable_OnlyEnsuresTheFullGammaRange()
    {
        var hardware = new FakeHardware();
        var runtime = Runtime(hardware);

        var report = runtime.Start();

        Assert.True(report.FullGammaRange);
        Assert.False(report.HasCalibration);
        Assert.True(report.TableApplied);
        Assert.Equal(ColorPipeline.FullGammaRange, hardware.GammaRange.Read());
        Assert.Equal(1, hardware.GammaRange.WriteCount);
        Assert.Empty(hardware.Gpu.Writes);   // tablo zaten birim: gereksiz geri alma noktası yok
        Assert.Empty(hardware.Monitor.Writes);
        Assert.Equal(0, hardware.Nvidia.WriteCount);
        Assert.Empty(hardware.Modes.Switches); // açılış Hz'e dokunmaz
    }

    [Fact]
    public void Start_AppliesTheSavedCalibration()
    {
        var hardware = new FakeHardware();
        var first = Runtime(hardware);
        first.Start();
        first.Wizard.Begin();
        Assert.True(first.Wizard.SetCalibration(SomeCalibration()));
        first.Wizard.Finish();
        var saved = hardware.Gpu.Read(FakeHardware.Display); // boru hattının kalibrasyon katmanından yazdığı tablo
        first.Shutdown();
        hardware.Gpu.SimulateExternalChange(FakeHardware.Display, GammaRamp.Identity); // yeniden açılış: ekranda birim tablo var

        var second = Runtime(hardware);
        var report = second.Start();

        Assert.True(report.HasCalibration);
        Assert.True(report.TableApplied);
        Assert.Equal(saved, hardware.Gpu.Read(FakeHardware.Display));
        Assert.False(IsIdentity(hardware.Gpu.Read(FakeHardware.Display)));
    }

    [Fact]
    public void Start_AfterACrash_ReplacesALeftOverEffectTableWithTheUsersTable()
    {
        var hardware = new FakeHardware();
        var leftOver = GammaRamps.Scale(new RgbGains(0.3, 0.3, 0.3));
        hardware.Gpu.SimulateExternalChange(FakeHardware.Display, leftOver); // çökmüş oturumun ek karartması
        var runtime = Runtime(hardware);

        var report = runtime.Start();

        Assert.True(report.TableApplied);
        Assert.True(IsIdentity(hardware.Gpu.Read(FakeHardware.Display)));
    }

    [Fact]
    public void Start_ReportsWhatFailed_AndKeepsGoing()
    {
        var hardware = new FakeHardware();
        var first = Runtime(hardware);
        first.Start();
        first.Wizard.Begin();
        first.Wizard.SetCalibration(SomeCalibration());
        first.Wizard.Finish();
        first.Shutdown();
        hardware.Gpu.SimulateExternalChange(FakeHardware.Display, GammaRamp.Identity);
        hardware.Gpu.RejectWrites = true;                       // Windows tabloyu reddediyor

        var second = Runtime(hardware);
        var report = second.Start();

        Assert.True(report.HasCalibration);
        Assert.False(report.TableApplied);
        Assert.True(IsIdentity(hardware.Gpu.Read(FakeHardware.Display))); // ekranda önceki tablo kaldı
    }

    [Fact]
    public void Start_ReportsADeniedAdminPrompt()
    {
        var hardware = new FakeHardware();
        hardware.GammaRange.AdminApproves = false;
        var runtime = Runtime(hardware);

        var report = runtime.Start();

        Assert.False(report.FullGammaRange);
        Assert.Null(hardware.GammaRange.Read());
    }

    [Fact]
    public void Start_CalledTwice_DoesNotWriteAgain()
    {
        var hardware = new FakeHardware();
        var runtime = Runtime(hardware);
        var first = runtime.Start();
        var writes = hardware.GammaRange.WriteCount + hardware.Gpu.Writes.Count;

        var second = runtime.Start();

        Assert.Equal(first, second);
        Assert.Equal(writes, hardware.GammaRange.WriteCount + hardware.Gpu.Writes.Count);
    }

    // --- Zamanlayıcı ---

    [Fact]
    public void Tick_RestoresTheUsersTable_WhenAnotherProgramChangedIt()
    {
        var hardware = new FakeHardware();
        var runtime = Runtime(hardware);
        runtime.Start();
        runtime.Brightness.Commit(0.05);
        var users = hardware.Gpu.Read(FakeHardware.Display);
        Assert.False(runtime.Tick().ExternalTableRestored); // kendi yazması dış değişiklik değil

        hardware.Gpu.SimulateExternalChange(FakeHardware.Display, GammaRamp.Identity); // oyun / Gece ışığı

        var tick = runtime.Tick();

        Assert.True(tick.ExternalTableRestored);
        Assert.Equal(users, hardware.Gpu.Read(FakeHardware.Display));
        Assert.False(runtime.Tick().ExternalTableRestored); // uyarı bir kez
    }

    [Fact]
    public void Tick_WritesNothing_WhenNothingChanged()
    {
        var hardware = new FakeHardware();
        var runtime = Runtime(hardware);
        runtime.Start();
        var writes = hardware.Gpu.Writes.Count;

        var tick = runtime.Tick();

        Assert.False(tick.ExternalTableRestored);
        Assert.False(tick.BlueLightChanged);
        Assert.Equal(writes, hardware.Gpu.Writes.Count);
        Assert.Empty(hardware.Monitor.Writes);
    }

    // --- Kapanış ---

    [Fact]
    public void Shutdown_RemovesEffects_KeepsCalibration_AndRestoresNvidia()
    {
        var hardware = new FakeHardware();
        var first = Runtime(hardware);
        first.Start();
        first.Wizard.Begin();
        first.Wizard.SetCalibration(SomeCalibration());
        first.Wizard.Finish();
        var calibration = hardware.Gpu.Read(FakeHardware.Display); // yalnız kalibrasyon katmanı varken ekrandaki tablo
        first.BlueLight.SetKelvin(3400);
        first.Brightness.Commit(0.05);
        Assert.NotEqual(calibration, hardware.Gpu.Read(FakeHardware.Display));

        first.Shutdown();

        Assert.Equal(calibration, hardware.Gpu.Read(FakeHardware.Display)); // sadece kalibrasyon kalır
        Assert.Equal(FakeNvidiaSettings.DriverDefault, hardware.Nvidia.Read(FakeHardware.Display));
    }

    [Fact]
    public void Shutdown_WithoutCalibration_LeavesAnIdentityTable()
    {
        var hardware = new FakeHardware();
        var runtime = Runtime(hardware);
        runtime.Start();
        runtime.BlueLight.SetKelvin(2700);
        runtime.Brightness.Commit(0.05);

        runtime.Shutdown();

        Assert.True(IsIdentity(hardware.Gpu.Read(FakeHardware.Display)));
        Assert.False(runtime.Brightness.DimmingActive);
    }

    [Fact]
    public void Shutdown_CancelsAnOpenWizard_SoItsSettingsGoBack()
    {
        var hardware = new FakeHardware();
        var runtime = Runtime(hardware);
        runtime.Start();
        var before = runtime.DdcPanel.Refresh().Settings.First(s => s.Setting == PanelSetting.Sharpness).Current;
        runtime.Wizard.Begin();
        Assert.Equal(PanelSetResult.Written, runtime.Wizard.SetDdc(PanelSetting.Sharpness, before == 1 ? 2 : 1));
        hardware.Clock.Advance(TimeSpan.FromSeconds(2)); // DDC yazma sınırı: aynı koda 1 sn içinde ikinci yazma yok

        runtime.Shutdown();

        Assert.False(runtime.Wizard.IsActive);
        Assert.Equal(before, runtime.DdcPanel.Refresh().Settings.First(s => s.Setting == PanelSetting.Sharpness).Current);
    }

    [Fact]
    public void Shutdown_CalledTwice_WritesNothingTheSecondTime()
    {
        var hardware = new FakeHardware();
        var runtime = Runtime(hardware);
        runtime.Start();
        runtime.Brightness.Commit(0.05);
        runtime.Shutdown();
        var writes = (hardware.Gpu.Writes.Count, hardware.Monitor.Writes.Count, hardware.Nvidia.WriteCount, hardware.GammaRange.WriteCount);

        runtime.Shutdown();

        Assert.Equal(writes, (hardware.Gpu.Writes.Count, hardware.Monitor.Writes.Count, hardware.Nvidia.WriteCount, hardware.GammaRange.WriteCount));
    }

    [Fact]
    public void Shutdown_NeverTouchesRefreshRateOrGammaRange()
    {
        var hardware = new FakeHardware(75, 76);
        var runtime = Runtime(hardware);
        runtime.Start();
        var gammaWrites = hardware.GammaRange.WriteCount;

        runtime.Shutdown();

        Assert.Empty(hardware.Modes.Switches);
        Assert.Equal(0, hardware.Modes.PersistCount);
        Assert.Equal(gammaWrites, hardware.GammaRange.WriteCount); // GdiIcmGammaRange sadece kaldırmada döner
    }
}
