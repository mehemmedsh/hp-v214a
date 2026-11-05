using MonitorTuner.Color;
using MonitorTuner.Core;

namespace MonitorTuner.Services;

/// <summary>Temel ayarın depodaki hâli (ayar deposu, "Baseline" bölümü): DDC kodları onaltılık metin anahtarlı.</summary>
public sealed record BaselineRecord(Dictionary<string, int> Ddc, ushort[] Red, ushort[] Green, ushort[] Blue);

/// <summary>
/// Ayar sihirbazı (T312, T313): desenlerle monitör ayarları (parlaklık, kontrast, keskinlik, beyaz noktası) ve gözle
/// kalibrasyon (gamma, gri dengesi). Başlarken mavi ışık, ek karartma, ton eğrisi (T320) ve canlılık geçici kapanır, bitince ya da iptalde geri gelir
/// (02-MIMARI.md → Ayar sahipliği, 4). DDC yazmaları yalnız <see cref="IDdcPanel"/> üzerinden gider (sihirbaz Güvenlik katmanını
/// ve DDC koruma kurallarını atlamaz); kalibrasyon yalnız <see cref="IColorPipeline"/>'ın kalibrasyon katmanına yazılır.
/// Sihirbaz hiçbir ayarı kendiliğinden değiştirmez: her yazma kullanıcının bir eylemidir. Tek iş parçacığından çağrılır (arayüz).
/// </summary>
public sealed class SetupWizard : ISetupWizard
{
    public const string Section = "Baseline";

    /// <summary>Arka ışık ilk dakikalarda kayar (08-GORUNTU-YONTEMLERI.md): ayara başlamadan önce önerilen süre.</summary>
    public static readonly TimeSpan WarmUp = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Sihirbazın dokunabildiği ayarlar. Renk ön ayarı yok: profilde Custom RGB henüz doğrulanmadı (estimated), iptalde
    /// geri yazılamazdı; beyaz eşleme için kullanıcı OSD'den Custom RGB'yi seçer, değilse kazanç <see cref="PanelSetResult.Locked"/> döner.
    /// </summary>
    private static readonly PanelSetting[] Tunable =
    [
        PanelSetting.Brightness, PanelSetting.Contrast, PanelSetting.Sharpness,
        PanelSetting.RedGain, PanelSetting.GreenGain, PanelSetting.BlueGain,
    ];

    private readonly IDdcPanel _panel;
    private readonly IBrightnessControl _brightness;
    private readonly IBlueLightFilter _blueLight;
    private readonly INvidiaControl _nvidia;
    private readonly IToneControl _tone;
    private readonly IColorPipeline _pipeline;
    private readonly ICalibrationCurves _curves;
    private readonly ISettingsStore _store;
    private readonly MonitorKey _key;
    private readonly TimeProvider _time;

    private readonly Dictionary<PanelSetting, int> _initial = [];
    private readonly HashSet<PanelSetting> _written = [];
    private DateTimeOffset _started;

    private GammaRamp? _previousCalibration; // Begin'deki kayıtlı kalibrasyon (iptalde ve seçimsiz bitişte geri gelir)
    private GammaRamp? _chosenCalibration;   // sihirbazda seçilip ekrana verilen son kalibrasyon
    private bool _calibrationTouched;        // Begin eski kalibrasyonu kapattı ya da bir seçim denendi

    public SetupWizard(
        IDdcPanel panel, IBrightnessControl brightness, IBlueLightFilter blueLight, INvidiaControl nvidia, IToneControl tone,
        IColorPipeline pipeline, ICalibrationCurves curves, ISettingsStore store, MonitorKey key, TimeProvider time)
    {
        _panel = panel;
        _brightness = brightness;
        _blueLight = blueLight;
        _nvidia = nvidia;
        _tone = tone;
        _pipeline = pipeline;
        _curves = curves;
        _store = store;
        _key = key;
        _time = time;
    }

    public IReadOnlyList<WizardStep> Steps { get; } =
    [
        WizardStep.WarmUpAndPosition, WizardStep.BlackAndWhiteClipping, WizardStep.Sharpness, WizardStep.FullRgbCheck,
        WizardStep.ViewingAngle, WizardStep.DeadPixelsAndUniformity, WizardStep.WhiteMatch,
        WizardStep.Gamma, WizardStep.GreyBalance,
    ];

    public bool IsActive { get; private set; }

    public TimeSpan WarmUpRemaining =>
        IsActive ? Max(TimeSpan.Zero, WarmUp - (_time.GetUtcNow() - _started)) : WarmUp;

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    public void Begin()
    {
        if (IsActive) return;

        // Önce efektler: mavi ışık kazançları eski değerine döndürür, ek karartma arka ışık kilidini kaldırır, ton eğrisi
        // kalkar (gamma desenleri sinema eğrisiyle ölçülseydi kalibrasyon onu "düzeltirdi"). Değerler bundan sonra okunur ki
        // iptalde döneceğimiz şey kullanıcının gerçek ayarı olsun.
        _blueLight.Suspend();
        _brightness.Suspend();
        _tone.Suspend();
        _nvidia.Suspend();
        _initial.Clear();
        _written.Clear();
        foreach (var s in _panel.Refresh().Settings.Where(s => Tunable.Contains(s.Setting)))
            _initial[s.Setting] = s.Current;
        _started = _time.GetUtcNow();

        // Eski kalibrasyon açıkken gamma ve gri dengesi düzeltilmiş ekranda ölçülürdü: ham panele dönülür.
        _previousCalibration = LoadBaseline()?.Calibration;
        _chosenCalibration = null;
        _calibrationTouched = false;
        if (_previousCalibration is not null && !_previousCalibration.Equals(GammaRamp.Identity))
            ApplyCalibration(null);

        IsActive = true;
    }

    public PanelSetResult SetDdc(PanelSetting setting, int value)
    {
        RequireActive();
        if (!Tunable.Contains(setting)) return PanelSetResult.NotSupported;

        var result = _panel.Set(setting, value);
        if (result == PanelSetResult.Written) _written.Add(setting);
        return result;
    }

    public bool SetCalibration(CalibrationChoices choices)
    {
        RequireActive();
        var table = _curves.Build(choices); // sınır dışı seçim burada atar, hiçbir şey yazılmaz
        if (ApplyCalibration(table))
        {
            _chosenCalibration = table;
            return true;
        }

        ApplyCalibrationLayer(_chosenCalibration); // reddedilen tablo katmanda kalmasın; sonraki Apply yine onu denerdi
        return false;
    }

    public Baseline Finish()
    {
        RequireActive();

        var values = Current().ToDictionary(s => DdcPanel.CodeOf(s.Setting), s => (ushort)s.Current);
        var calibration = _chosenCalibration ?? _previousCalibration ?? GammaRamp.Identity;
        if (_calibrationTouched) ApplyCalibration(calibration); // seçim yoksa Begin'in kapattığı eski kalibrasyon geri gelir
        var baseline = new Baseline(values, calibration);
        _store.Write(_key, Section, new BaselineRecord(
            values.ToDictionary(p => p.Key.ToString("X2"), p => (int)p.Value),
            [.. calibration.Red], [.. calibration.Green], [.. calibration.Blue]));

        Leave(backlightChanged: _written.Contains(PanelSetting.Brightness));
        return baseline;
    }

    public IReadOnlyList<PanelSetting> Cancel()
    {
        if (!IsActive) return [];

        var failed = new List<PanelSetting>();
        foreach (var setting in Tunable.Where(_written.Contains))
        {
            if (_panel.Set(setting, _initial[setting]) is not (PanelSetResult.Written or PanelSetResult.Unchanged))
                failed.Add(setting);
        }

        if (_calibrationTouched) ApplyCalibration(_previousCalibration); // geri dönemezse ekran son Apply'daki hâlinde kalır
        Leave(backlightChanged: false); // eski arka ışığa dönüldü: saklanan kaydırıcı yine doğru
        return failed;
    }

    public Baseline? LoadBaseline()
    {
        if (_store.Read<BaselineRecord>(_key, Section) is not { Ddc: not null, Red.Length: GammaRamp.Size, Green.Length: GammaRamp.Size, Blue.Length: GammaRamp.Size } r)
            return null;
        var values = r.Ddc.ToDictionary(p => Convert.ToByte(p.Key, 16), p => (ushort)p.Value);
        return new Baseline(values, new GammaRamp(r.Red, r.Green, r.Blue));
    }

    private IEnumerable<PanelSettingState> Current() =>
        _panel.Refresh().Settings.Where(s => Tunable.Contains(s.Setting));

    /// <summary>
    /// Efektler geri gelir: önce DDC yazıldı, mavi ışık yeni kazançları taban alır. Sihirbaz arka ışığı değiştirdiyse
    /// eski kaydırıcının ek karartması geri gelmez (yeni arka ışıkla üst üste biner, ekran koyulaşır): kaydırıcı yeni
    /// arka ışıktan kurulur. Katman askıda olduğundan <see cref="IBrightnessControl.ReadSlider"/> arka ışığın yerini verir.
    /// </summary>
    private void Leave(bool backlightChanged)
    {
        IsActive = false;
        _blueLight.Resume();
        _tone.Resume();
        _nvidia.Resume();
        if (backlightChanged && _brightness.ReadSlider() is { } slider) _brightness.ResumeAt(slider);
        else _brightness.Resume();
    }

    /// <summary>Kalibrasyon katmanını verip yazar (null ya da birim tablo: katman kapalı). false: Windows reddetti ya da tablo okunamadı.</summary>
    private bool ApplyCalibration(GammaRamp? table)
    {
        _calibrationTouched = true;
        return ApplyCalibrationLayer(table);
    }

    private bool ApplyCalibrationLayer(GammaRamp? table)
    {
        _pipeline.SetLayer(ColorLayer.Calibration, table is null || table.Equals(GammaRamp.Identity) ? null : table);
        return _pipeline.Apply();
    }

    private void RequireActive()
    {
        if (!IsActive) throw new InvalidOperationException("Sihirbaz başlamadı: önce Begin().");
    }
}
