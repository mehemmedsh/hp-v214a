using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Core.Profiles;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;

namespace MonitorTuner.Services;

/// <summary>
/// Uygulamanın donanım kapıları. Gerçek Windows sınıfları <c>MonitorTuner.Composition</c>'da verilir, testlerde sahteler.
/// </summary>
/// <param name="ConflictProbe">Gece ışığı / f.lux tespiti (mavi ışık filtresi uyarısı).</param>
public sealed record AppPorts(
    IDisplayEnumerator Displays, IDdcMonitor Ddc, IGpuGammaTable GpuTable, IDisplayModes Modes,
    INvidiaSettings Nvidia, IGdiIcmGammaRange GammaRange, IColorConflictProbe ConflictProbe);

/// <summary><see cref="AppRuntime.Create"/> sonucu. Runtime null: bağlı ekranlardan hiçbiri bir profille eşleşmedi, hiçbir şey yazılmadı.</summary>
/// <param name="Monitors">Görülen ekranlar (arayüz "şunları gördüm, desteklenen yok" diyebilsin).</param>
public sealed record AppLaunch(AppRuntime? Runtime, IReadOnlyList<MonitorInfo> Monitors);

/// <param name="FullGammaRange">GdiIcmGammaRange 256 (yönetici onayı verilmediyse false; Windows derin karartmayı reddedebilir).</param>
/// <param name="HasCalibration">Kayıtlı kalibrasyon (ayar sihirbazının temel ayarı) var.</param>
/// <param name="TableApplied">Açılıştaki tablo yazıldı (ya da zaten ekrandaydı). false: Windows tabloyu reddetti, ekranda önceki tablo kaldı.</param>
public sealed record StartupReport(bool FullGammaRange, bool HasCalibration, bool TableApplied);

/// <param name="ExternalTableRestored">Başka program GPU tablosunu değiştirmişti, kullanıcının tablosu geri yüklendi (kullanıcıya uyarı).</param>
/// <param name="BlueLightChanged">İsteğe bağlı akşam geçişi bir adım ilerledi.</param>
public sealed record TickReport(bool ExternalTableRestored, bool BlueLightChanged);

/// <summary>
/// Bağlama (T314): bir monitör için bütün servisleri kurar ve açılış, zamanlayıcı ve kapanış sırasını yönetir. Donanım kapıları
/// arayüzdür; gerçek Windows sınıfları <c>MonitorTuner.Composition</c>'dan gelir, böylece bağlama sahte donanımla test edilir.
/// <see cref="Create"/> hiçbir şeye yazmaz; yazma <see cref="Start"/>, kullanıcı eylemleri ve <see cref="Shutdown"/> ile olur.
/// Tek iş parçacığından çağrılır (arayüz).
/// </summary>
public sealed class AppRuntime
{
    private readonly IColorPipeline _pipeline;
    private readonly INvidiaControl _nvidia;

    private StartupReport? _started;
    private bool _shutDown;

    private AppRuntime(
        MonitorInfo monitor, MonitorProfile profile, IColorPipeline pipeline, INvidiaControl nvidia, IBrightnessControl brightness,
        IBlueLightFilter blueLight, IToneControl tone, IDdcPanel ddcPanel, IRefreshRatePanel refreshRates, ISetupWizard wizard,
        IColorProfiles profiles, IQuickControls quickControls, IBreakReminder breaks)
    {
        Breaks = breaks;
        (Monitor, Profile, _pipeline, _nvidia) = (monitor, profile, pipeline, nvidia);
        (Brightness, BlueLight, Tone, DdcPanel, RefreshRates) = (brightness, blueLight, tone, ddcPanel, refreshRates);
        (Wizard, Profiles, QuickControls) = (wizard, profiles, quickControls);
    }

    public MonitorInfo Monitor { get; }
    public MonitorProfile Profile { get; }
    public IBrightnessControl Brightness { get; }
    public IBlueLightFilter BlueLight { get; }

    /// <summary>Ton eğrisi (T320): sinema gamması ya da canlı kontrast; renk profilleri kurar, panik kapatır.</summary>
    public IToneControl Tone { get; }

    public IDdcPanel DdcPanel { get; }
    public IRefreshRatePanel RefreshRates { get; }
    public ISetupWizard Wizard { get; }
    public IColorProfiles Profiles { get; }

    /// <summary>Tepsi ve ana pencerenin ortak giriş noktası (profil seçimi, parlaklık, mavi ışık, panik).</summary>
    public IQuickControls QuickControls { get; }

    /// <summary>İsteğe bağlı 20-20-20 mola hatırlatıcısı (T324); ekran ayarına dokunmaz.</summary>
    public IBreakReminder Breaks { get; }

    /// <summary>
    /// Bağlı ekranlardan bir profille eşleşen ilkini seçer ve servisleri kurar. Eşleşme yoksa runtime null döner; o ekranlara
    /// hiçbir zaman yazılmaz (bilinmeyen monitörün DDC kodları, EDID'i okunamayan ekran dahil).
    /// </summary>
    public static AppLaunch Create(AppPorts ports, IReadOnlyList<MonitorProfile> profiles, ISettingsStore store, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(ports);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(time);

        var monitors = new MonitorIdentifier(ports.Displays).Identify();
        foreach (var monitor in monitors)
        {
            var profile = profiles.FirstOrDefault(monitor.Matches);
            if (profile is not null) return new AppLaunch(Build(ports, monitor, profile, store, time), monitors);
        }
        return new AppLaunch(null, monitors);
    }

    private static AppRuntime Build(AppPorts ports, MonitorInfo monitor, MonitorProfile profile, ISettingsStore store, TimeProvider time)
    {
        var (display, key) = (monitor.Display, monitor.Key);

        // İlk açılış yedeği ayar deposunun yedek klasöründe: kurtarma dosyası (T104) yedeği aynı yerde arar.
        var factory = new JsonFactoryBackupStore(store.BackupFolder);
        var hardware = new SafetyHardware(ports.Ddc, ports.GpuTable, ports.Modes, ports.Nvidia, ports.GammaRange);

        // Güvenlik ↔ boru hattı döngüsü: Güvenlik boru hattını geç ister (kilit sırası boru hattı → Güvenlik).
        ColorPipeline? pipeline = null;
        var safety = new SafetyLayer(display, profile.WritableDdcCodes, hardware, () => pipeline!, factory, time);
        var nvidia = new NvidiaControl(display, ports.Nvidia, safety, factory);
        pipeline = new ColorPipeline(display, ports.GpuTable, ports.GammaRange, new GammaTableCombiner(), safety, nvidia);

        var ddc = new DdcControl(display, ports.Ddc, safety, profile, time);
        var brightness = new BrightnessControl(ddc, pipeline, new DimmingPlanner());
        var blueLight = new BlueLightFilter(profile, ddc, pipeline, new KelvinCalculator(), ports.ConflictProbe, time, store, key);
        var tone = new ToneControl(pipeline, new ToneCurves());
        var panel = new DdcPanel(profile, ddc, blueLight, store, key, monitor.Connection, () => brightness.DimmingActive);
        var wizard = new SetupWizard(panel, brightness, blueLight, nvidia, tone, pipeline, new CalibrationCurves(), store, key, time);
        var colorProfiles = new ColorProfiles(brightness, blueLight, nvidia, tone, store, key);
        var refreshRates = new RefreshRatePanel(display, profile, ports.Modes, ddc, safety, store, key);
        var quick = new QuickControls(colorProfiles, brightness, blueLight, wizard);

        var breaks = new BreakReminder(store, key, time);

        return new AppRuntime(
            monitor, profile, pipeline, nvidia, brightness, blueLight, tone, panel, refreshRates, wizard, colorProfiles, quick, breaks);
    }

    /// <summary>
    /// Açılış: GdiIcmGammaRange 256 olur (değer zaten 256 ise yazılmaz; ilk seferde tek yönetici onayı), kayıtlı kalibrasyon
    /// kalibrasyon katmanına verilir ve tablo yazılır. Kalibrasyon yoksa da yazılır: çökmüş bir oturumdan kalan ek karartma ya da mavi ışık
    /// tablosu böylece kullanıcının tablosuyla değişir (02-MIMARI.md → Renk boru hattı). Parlaklığa, Hz'e ve DDC'ye dokunmaz.
    /// İkinci çağrı bir şey yazmaz, aynı raporu verir. Bir adımın başarısızlığı diğerini durdurmaz; rapor söyler.
    /// </summary>
    public StartupReport Start()
    {
        if (_started is { } report) return report;

        var fullRange = Attempt(_pipeline.EnsureFullGammaRange); // geniş eğrileri Windows ancak 256 ile kabul eder
        var calibration = Attempt(Wizard.LoadBaseline)?.Calibration;
        if (calibration is not null) _pipeline.SetLayer(ColorLayer.Calibration, calibration);
        var applied = Attempt(_pipeline.Apply);
        return _started = new StartupReport(fullRange, calibration is not null, applied);
    }

    /// <summary>
    /// Zamanlayıcı (arayüz birkaç saniyede bir çağırır): başka program GPU tablosunu değiştirdiyse kullanıcının tablosunu geri yükler
    /// (Kural 1, 2. istisna); akşam geçişi açıksa sıradaki adımı uygular. Hz ve parlaklığa dokunmaz.
    /// </summary>
    public TickReport Tick()
    {
        if (_shutDown) return new TickReport(false, false);
        return new TickReport(Attempt(_pipeline.RestoreIfChangedExternally), Attempt(BlueLight.Tick));
    }

    /// <summary>
    /// Normal kapanış, sırayla: açık sihirbaz iptal edilir (ayarları eski değere döner), mavi ışık ve ek karartma kalkar,
    /// tabloda sadece kalibrasyon kalır (ton eğrisi de kalkar), dithering ve canlılık kullanıcının değerine döner. Hz, parlaklık (arka ışık) ve
    /// GdiIcmGammaRange'e dokunmaz. İkinci çağrı bir şey yapmaz. Bir adım hata verse de ötekiler yapılır.
    /// </summary>
    public void Shutdown()
    {
        if (_shutDown) return;
        _shutDown = true;

        if (Wizard.IsActive) Attempt(Wizard.Cancel);
        Attempt(BlueLight.Shutdown);
        Attempt(Brightness.Shutdown);
        Attempt(() => _pipeline.Shutdown());
        Attempt(() => _nvidia.Shutdown());
    }

    // Kapanış ve zamanlayıcı yarıda kalmamalı: sürücü, kayıt defteri ya da Güvenlik hatası o adımı başarısız sayar.
    private static bool Attempt(Func<bool> step) => Attempt<bool>(step);

    private static T? Attempt<T>(Func<T> step)
    {
        try
        {
            return step();
        }
        catch (Exception)
        {
            return default;
        }
    }

    private static void Attempt(Action step) => Attempt<object?>(() =>
    {
        step();
        return null;
    });
}
