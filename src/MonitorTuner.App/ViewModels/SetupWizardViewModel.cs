using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using MonitorTuner.App.Localization;
using MonitorTuner.Color;
using MonitorTuner.Services;

namespace MonitorTuner.App.ViewModels;

/// <summary>Sayfadaki bir desen düğmesi.</summary>
public sealed record PatternItem(Pattern Pattern, string Title);

/// <summary>Gamma adayı: değer ve desendeki grinin seviyesi (kullanıcı karenin altındaki sayıyla eşler).</summary>
public sealed record GammaChoice(double Gamma, string Title)
{
    public override string ToString() => Title;
}

/// <summary>
/// Kalibrasyon kaydırıcısı (yüzde; 100 = değişiklik yok). Sürüklerken sadece ekranda değişir; bırakınca
/// <see cref="SetupWizardViewModel.CommitGain"/> bütün kalibrasyonu bir kez yeniden kurar.
/// </summary>
public sealed class GainSliderViewModel(string title, int minimum, int maximum) : INotifyPropertyChanged
{
    private double _value = 100;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; } = title;
    public double Minimum { get; } = minimum;
    public double Maximum { get; } = maximum;

    public double Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(Math.Round(value), Minimum, Maximum);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValueText)));
        }
    }

    public string ValueText => "%" + ((int)_value).ToString(CultureInfo.CurrentCulture);

    internal double Factor => _value / 100;
}

/// <summary>
/// Ayar sihirbazı sayfasının mantığı (T315). Donanıma dokunmaz: yazmaların hepsi <see cref="ISetupWizard"/> üzerinden
/// (DDC değerleri <see cref="ISetupWizard.SetDdc"/>, kalibrasyon <see cref="ISetupWizard.SetCalibration"/>); iptal böylece
/// sihirbazın yazdığı her şeyi geri alabilir. <see cref="IDdcPanel"/> sadece okumak için (kaydırıcı sınırları, kilit).
/// Hiçbir şey kendiliğinden yazılmaz: gezinme ve desen açma yazmaz; kaydırıcı bırakılınca ya da seçim yapılınca tek yazma.
/// </summary>
public sealed class SetupWizardViewModel : INotifyPropertyChanged
{
    private static readonly IReadOnlyDictionary<WizardStep, PanelSetting[]> StepSettings = new Dictionary<WizardStep, PanelSetting[]>
    {
        [WizardStep.BlackAndWhiteClipping] = [PanelSetting.Contrast, PanelSetting.Brightness],
        [WizardStep.Sharpness] = [PanelSetting.Sharpness],
        [WizardStep.WhiteMatch] = [PanelSetting.RedGain, PanelSetting.GreenGain, PanelSetting.BlueGain],
    };

    private static readonly string[] Channels = ["R", "G", "B"];

    private readonly ISetupWizard _wizard;
    private readonly IDdcPanel _panel;
    private int _step;
    private bool _ddcAvailable = true;
    private GammaChoice? _gamma;
    private string _status = "";

    public SetupWizardViewModel(ISetupWizard wizard, IDdcPanel panel)
    {
        _wizard = wizard;
        _panel = panel;
        GammaChoices = [.. WizardPatterns.GammaCandidates.Select(g => new GammaChoice(g, string.Format(CultureInfo.CurrentCulture,
            Loc.Get("SetupWizard.Gamma_Choice"), g.ToString("0.0", CultureInfo.CurrentCulture), GreyLevel(g))))];
        GreyItems = [.. new[] { "Dark", "Mid", "Light" }.SelectMany(level => Channels.Select(c => new GainSliderViewModel(
            string.Format(CultureInfo.CurrentCulture, Loc.Get("SetupWizard.GainItem"), Loc.Get("SetupWizard.Grey_" + level), Loc.Get("SetupWizard.Channel_" + c)),
            80, 125)))]; // CalibrationCurves sınırları: gri 0,8–1,25
        GpuWhiteItems = [.. Channels.Select(c => new GainSliderViewModel(Loc.Get("SetupWizard.Channel_" + c), 80, 100))]; // beyaz sadece kısar
        IsActive = wizard.IsActive;
        if (IsActive) LoadStep();
        else Status = Loc.Get("SetupWizard.Idle");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Görünüm verir: deseni tam ekran açar (modal), kapanınca son gösterilen karenin sırasını döner.</summary>
    public Func<Pattern, int>? ShowPattern { get; set; }

    public bool IsActive { get; private set; }
    public bool IsIdle => !IsActive;

    public int StepIndex => _step;
    public WizardStep CurrentStep => _wizard.Steps[_step];
    public string StepTitle => Loc.Get($"SetupWizard.Step_{CurrentStep}_Title");
    public string StepText => Loc.Get($"SetupWizard.Step_{CurrentStep}_Text");
    public string StepCounter => string.Format(CultureInfo.CurrentCulture, Loc.Get("SetupWizard.StepCounter"), _step + 1, _wizard.Steps.Count);
    public bool CanGoBack => IsActive && _step > 0;
    public bool CanGoNext => IsActive && _step < _wizard.Steps.Count - 1;
    public bool IsLastStep => _step == _wizard.Steps.Count - 1;

    public bool ShowWarmUp => IsActive && CurrentStep == WizardStep.WarmUpAndPosition;

    public string WarmUpText
    {
        get
        {
            var remaining = _wizard.WarmUpRemaining;
            return remaining > TimeSpan.Zero
                ? string.Format(CultureInfo.CurrentCulture, Loc.Get("SetupWizard.WarmUpRemaining"), (int)Math.Ceiling(remaining.TotalMinutes))
                : Loc.Get("SetupWizard.WarmUpDone");
        }
    }

    public IReadOnlyList<PatternItem> Patterns { get; private set; } = [];
    public bool HasPatterns => Patterns.Count > 0;

    /// <summary>Bu adımın DDC ayarları (kırpma: kontrast, parlaklık; keskinlik; beyaz eşleme: RGB kazancı).</summary>
    public ObservableCollection<PanelItemViewModel> DdcItems { get; } = [];
    public bool ShowDdcItems => DdcItems.Count > 0;
    public bool ShowNoDdc => IsActive && !_ddcAvailable && StepSettings.ContainsKey(CurrentStep);

    /// <summary>DDC yoksa beyaz eşleme GPU'da (<see cref="CalibrationChoices.WhiteGains"/>).</summary>
    public IReadOnlyList<GainSliderViewModel> GpuWhiteItems { get; }
    public bool ShowGpuWhite => IsActive && !_ddcAvailable && CurrentStep == WizardStep.WhiteMatch;

    public bool IsGammaStep => IsActive && CurrentStep == WizardStep.Gamma;
    public IReadOnlyList<GammaChoice> GammaChoices { get; }

    /// <summary>Seçmek kullanıcının eylemidir: kalibrasyon hemen yeniden kurulur ve ekrana verilir.</summary>
    public GammaChoice? SelectedGamma
    {
        get => _gamma;
        set
        {
            if (value is null || value == _gamma || !IsActive) return;
            _gamma = value;
            Raise();
            ApplyCalibration();
        }
    }

    /// <summary>Gri dengesi: koyu, orta, açık gri × kırmızı, yeşil, mavi (sırayla).</summary>
    public IReadOnlyList<GainSliderViewModel> GreyItems { get; }
    public bool IsGreyBalanceStep => IsActive && CurrentStep == WizardStep.GreyBalance;

    public string Status
    {
        get => _status;
        private set
        {
            _status = value;
            Raise();
        }
    }

    public void Start()
    {
        if (IsActive) return;
        _wizard.Begin();
        _step = 0;
        _gamma = null;
        foreach (var item in GreyItems.Concat(GpuWhiteItems)) item.Value = 100;
        IsActive = true;
        Status = Loc.Get("SetupWizard.Status_Started");
        LoadStep();
    }

    public void Next() => Move(1);

    public void Back() => Move(-1);

    public void Finish()
    {
        if (!IsActive) return;
        _wizard.Finish();
        IsActive = false;
        Status = Loc.Get("SetupWizard.Status_Finished");
        LoadStep();
    }

    public void Cancel()
    {
        if (!IsActive) return;
        var failed = _wizard.Cancel();
        IsActive = false;
        Status = failed.Count == 0
            ? Loc.Get("SetupWizard.Status_Cancelled")
            : string.Format(CultureInfo.CurrentCulture, Loc.Get("SetupWizard.Status_CancelPartial"),
                string.Join(", ", failed.Select(s => Loc.Get("DdcPanel.Setting_" + s))));
        LoadStep();
    }

    public void RefreshWarmUp() => Raise(nameof(WarmUpText));

    /// <summary>Deseni açar (yazmaz). Gamma deseninde kapatılan kareyi söyler; seçim kullanıcının.</summary>
    public void OpenPattern(PatternItem item)
    {
        if (ShowPattern is not { } show) return;
        var frame = show(item.Pattern);
        if (item.Pattern.Kind == PatternKind.Gamma && frame >= 0 && frame < GammaChoices.Count)
            Status = string.Format(CultureInfo.CurrentCulture, Loc.Get("SetupWizard.Gamma_LastFrame"), GammaChoices[frame].Title);
    }

    /// <summary>DDC kaydırıcısı bırakıldı: sihirbaz üzerinden tek yazma, sonra monitörden yeniden okunur.</summary>
    public void CommitDdc(PanelItemViewModel item)
    {
        if (!IsActive) return;
        var result = _wizard.SetDdc(item.Setting, item.ValueToWrite);
        if (result != PanelSetResult.Unchanged) Status = Loc.Get("DdcPanel.Result_" + result); // çift bırakma olayı sonucu ezmesin
        LoadDdcItems();
    }

    /// <summary>Gri dengesi ya da GPU beyaz kaydırıcısı bırakıldı: kalibrasyon bir kez yeniden kurulur.</summary>
    public void CommitGain(GainSliderViewModel item)
    {
        if (IsActive) ApplyCalibration();
    }

    private void ApplyCalibration()
    {
        RgbGains Gains(int offset, IReadOnlyList<GainSliderViewModel> items) =>
            new(items[offset].Factor, items[offset + 1].Factor, items[offset + 2].Factor);

        var white = !_ddcAvailable && GpuWhiteItems.Any(g => g.Value != 100) ? Gains(0, GpuWhiteItems) : (RgbGains?)null;
        var choices = new CalibrationChoices(_gamma?.Gamma ?? CalibrationChoices.TargetGamma,
            Gains(0, GreyItems), Gains(3, GreyItems), Gains(6, GreyItems), white);
        try
        {
            Status = Loc.Get(_wizard.SetCalibration(choices) ? "SetupWizard.Status_CalibrationApplied" : "SetupWizard.Status_CalibrationRejected");
        }
        catch (ArgumentOutOfRangeException)
        {
            Status = Loc.Get("SetupWizard.Status_CalibrationOutOfRange");
        }
    }

    private void Move(int delta)
    {
        if (!IsActive) return;
        var next = Math.Clamp(_step + delta, 0, _wizard.Steps.Count - 1);
        if (next == _step) return;
        _step = next;
        LoadStep();
    }

    private void LoadStep()
    {
        Patterns = IsActive ? [.. WizardPatterns.For(CurrentStep).Select(p => new PatternItem(p,
            string.Format(CultureInfo.CurrentCulture, Loc.Get("SetupWizard.OpenPattern"), Loc.Get("SetupWizard.Pattern_" + p.Kind))))] : [];
        LoadDdcItems();
        Raise(string.Empty); // bütün özellikler adıma bağlı
    }

    private void LoadDdcItems()
    {
        DdcItems.Clear();
        if (IsActive && StepSettings.TryGetValue(CurrentStep, out var settings))
        {
            var state = _panel.Refresh();
            _ddcAvailable = state.Available;
            foreach (var setting in settings)
                if (state.Settings.FirstOrDefault(s => s.Setting == setting) is { } s) DdcItems.Add(new PanelItemViewModel(s, CommitDdc));
        }

        Raise(nameof(ShowDdcItems));
        Raise(nameof(ShowNoDdc));
        Raise(nameof(ShowGpuWhite));
    }

    private static int GreyLevel(double gamma) => (int)Math.Round(255 * Math.Pow(0.5, 1 / gamma)); // WizardPatterns.Gamma ile aynı

    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
