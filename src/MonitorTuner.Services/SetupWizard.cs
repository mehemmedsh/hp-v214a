using MonitorTuner.Color;
using MonitorTuner.Core;

namespace MonitorTuner.Services;

/// <summary>Temel ayarın depodaki hâli (ayar deposu, "Baseline" bölümü): DDC kodları onaltılık metin anahtarlı.</summary>
public sealed record BaselineRecord(Dictionary<string, int> Ddc, ushort[] Red, ushort[] Green, ushort[] Blue);

/// <summary>
/// Ayar sihirbazı 1 (T312): desenlerle monitör ayarları (parlaklık, kontrast, keskinlik, beyaz noktası). Başlarken mavi ışık ve
/// ek karartma geçici kapanır, bitince ya da iptalde geri gelir (02-MIMARI.md → Ayar sahipliği, 4). Yazmalar yalnız
/// <see cref="IDdcPanel"/> üzerinden gider; sihirbaz Güvenlik katmanını ve DDC koruma kurallarını atlamaz.
/// Sihirbaz hiçbir ayarı kendiliğinden değiştirmez: her yazma kullanıcının bir eylemidir. Tek iş parçacığından çağrılır (arayüz).
/// </summary>
public sealed class SetupWizard : ISetupWizard
{
    public const string Section = "Baseline";

    /// <summary>Arka ışık ilk dakikalarda kayar (08-GORUNTU-YONTEMLERI.md): ayara başlamadan önce önerilen süre.</summary>
    public static readonly TimeSpan WarmUp = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Sihirbazın dokunabildiği ayarlar. Renk ön ayarı yok: ön ayar değişince monitör parlaklığı da kendi kayıtlı değerine
    /// geçer (DDC 10 ön ayara bağlı), taban değerler karışırdı; beyaz eşleme için kullanıcı OSD'den Custom RGB'yi seçer,
    /// değilse kazanç <see cref="PanelSetResult.Locked"/> döner.
    /// </summary>
    private static readonly PanelSetting[] Tunable =
    [
        PanelSetting.Brightness, PanelSetting.Contrast, PanelSetting.Sharpness,
        PanelSetting.RedGain, PanelSetting.GreenGain, PanelSetting.BlueGain,
    ];

    private readonly IDdcPanel _panel;
    private readonly IBrightnessControl _brightness;
    private readonly IBlueLightFilter _blueLight;
    private readonly ISettingsStore _store;
    private readonly MonitorKey _key;
    private readonly TimeProvider _time;

    private readonly Dictionary<PanelSetting, int> _initial = [];
    private readonly HashSet<PanelSetting> _written = [];
    private DateTimeOffset _started;

    public SetupWizard(
        IDdcPanel panel, IBrightnessControl brightness, IBlueLightFilter blueLight, ISettingsStore store, MonitorKey key,
        TimeProvider time)
    {
        _panel = panel;
        _brightness = brightness;
        _blueLight = blueLight;
        _store = store;
        _key = key;
        _time = time;
    }

    public IReadOnlyList<WizardStep> Steps { get; } =
    [
        WizardStep.WarmUpAndPosition, WizardStep.BlackAndWhiteClipping, WizardStep.Sharpness, WizardStep.FullRgbCheck,
        WizardStep.ViewingAngle, WizardStep.DeadPixelsAndUniformity, WizardStep.WhiteMatch,
    ];

    public bool IsActive { get; private set; }

    public TimeSpan WarmUpRemaining =>
        IsActive ? Max(TimeSpan.Zero, WarmUp - (_time.GetUtcNow() - _started)) : WarmUp;

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    public void Begin()
    {
        if (IsActive) return;

        // Önce efektler: mavi ışık kazançları eski değerine döndürür, ek karartma arka ışık kilidini kaldırır.
        // Değerler bundan sonra okunur ki iptalde döneceğimiz şey kullanıcının gerçek ayarı olsun.
        _blueLight.Suspend();
        _brightness.Suspend();
        _initial.Clear();
        _written.Clear();
        foreach (var s in _panel.Refresh().Settings.Where(s => Tunable.Contains(s.Setting)))
            _initial[s.Setting] = s.Current;
        _started = _time.GetUtcNow();
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

    public void SetCalibration(CalibrationChoices choices) =>
        throw new NotSupportedException("Gözle kalibrasyon (gamma, gri dengesi) T313'te.");

    public Baseline Finish()
    {
        RequireActive();

        var values = Current().ToDictionary(s => DdcPanel.CodeOf(s.Setting), s => (ushort)s.Current);
        var calibration = LoadBaseline()?.Calibration ?? GammaRamp.Identity; // T313 yazana kadar eski temel ayarın kalibrasyonu
        var baseline = new Baseline(values, calibration);
        _store.Write(_key, Section, new BaselineRecord(
            values.ToDictionary(p => p.Key.ToString("X2"), p => (int)p.Value),
            [.. calibration.Red], [.. calibration.Green], [.. calibration.Blue]));

        Leave();
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

        Leave();
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

    /// <summary>Efektler geri gelir: önce DDC yazıldı, mavi ışık yeni kazançları taban alır.</summary>
    private void Leave()
    {
        IsActive = false;
        _blueLight.Resume();
        _brightness.Resume();
    }

    private void RequireActive()
    {
        if (!IsActive) throw new InvalidOperationException("Sihirbaz başlamadı: önce Begin().");
    }
}
