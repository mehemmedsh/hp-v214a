using Microsoft.Extensions.Time.Testing;
using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Core.Profiles;
using MonitorTuner.Services;
using MonitorTuner.Tests.Fakes;

namespace MonitorTuner.Tests.Contracts;

/// <summary>
/// Ayar sihirbazı 1 (T312, 02-MIMARI.md). Sahte monitör ve gerçek DDC paneli üzerinden: başlarken efektler kapanır,
/// bitince geri gelir; adımlar DDC paneliyle (dijital parlaklık koruması dahil) yazar; sonuç temel ayar olarak kaydedilir.
/// </summary>
public class SetupWizardTests
{
    private static readonly MonitorProfile Profile =
        new MonitorProfileReader().Read(File.ReadAllText(Path.Combine(RepoRoot.Find(), "profiles", "hp-p21b-g4.json")));

    private static readonly MonitorKey Key = new("HP P21b G4", "SN1", "HDMI", "RTX 3080");
    private const byte Brightness = 0x10, Contrast = 0x12, Preset = 0x14, Red = 0x16, Green = 0x18, Blue = 0x1A, Sharpness = 0x87;
    private const int CustomRgb = 11;

    private sealed class FakeStore : ISettingsStore
    {
        public Dictionary<string, object> Sections { get; } = [];
        public string BackupFolder => "";
        public T? Read<T>(MonitorKey monitor, string section) => Sections.TryGetValue(section, out var v) && v is T t ? t : default;
        public void Write<T>(MonitorKey monitor, string section, T value) => Sections[section] = value!;
    }

    private sealed class StubBlueLight : IBlueLightFilter
    {
        public int Kelvin { get; set; } = 4000; // mavi ışık açık: RGB kazancı normalde kilitli
        public bool IsSuspended { get; set; }
        public int SuspendCalls { get; private set; }
        public int ResumeCalls { get; private set; }
        public EveningSettings Evening { get; set; } = EveningSettings.Default;
        public BlueLightState Suspend() { SuspendCalls++; IsSuspended = true; return Idle(); }
        public BlueLightState Resume() { ResumeCalls++; IsSuspended = false; return Idle(); }
        public BlueLightState SetKelvin(int kelvin) => throw new NotSupportedException();
        public BlueLightState Disable() => throw new NotSupportedException();
        public BlueLightState Shutdown() => throw new NotSupportedException();
        public LowBlueLightResult SelectMonitorLowBlueLight() => throw new NotSupportedException();
        public IReadOnlyList<ColorConflict> CheckConflicts() => [];
        public bool Tick() => false;
        private BlueLightState Idle() => new(Kelvin, true, RgbGains.Neutral, RgbGains.Neutral, []);
    }

    private sealed class StubBrightness : IBrightnessControl
    {
        public bool DimmingActive { get; private set; } = true; // ek karartma açık: arka ışık normalde kilitli
        public int SuspendCalls { get; private set; }
        public int ResumeCalls { get; private set; }
        public List<double> ResumeAtCalls { get; } = [];
        public bool Suspend() { SuspendCalls++; DimmingActive = false; return true; }
        public bool Resume() { ResumeCalls++; DimmingActive = true; return true; }
        public bool ResumeAt(double slider) { ResumeAtCalls.Add(slider); return true; }
        public DimmingPlan Preview(double slider) => throw new NotSupportedException();
        public BrightnessState Commit(double slider) => throw new NotSupportedException();
        public double? ReadSlider() => 0.5;
        public bool DisableDimming() => throw new NotSupportedException();
        public bool Shutdown() => throw new NotSupportedException();
    }

    /// <summary>Katmanların son tablosunu ve <see cref="Apply"/> sayısını tutar (gerçek <see cref="BrightnessControl"/> ile).</summary>
    private sealed class RecordingPipeline : IColorPipeline
    {
        public GammaRamp? Dimming { get; private set; }
        public GammaRamp? Calibration { get; private set; }
        public int Applies { get; private set; }
        public bool ApplyResult { get; set; } = true;
        public void SetLayer(ColorLayer layer, GammaRamp? table)
        {
            if (layer == ColorLayer.Dimming) Dimming = table;
            if (layer == ColorLayer.Calibration) Calibration = table;
        }
        public bool Apply() { Applies++; return ApplyResult; }
        public bool RestoreIfChangedExternally() => throw new NotSupportedException();
        public bool EnsureFullGammaRange() => throw new NotSupportedException();
        public void Shutdown() => throw new NotSupportedException();
        public void ResetTable() => throw new NotSupportedException();
    }

    private sealed class StubNvidia : INvidiaControl
    {
        public int? Vibrance => null;
        public int SuspendCalls { get; private set; }
        public int ResumeCalls { get; private set; }
        public bool SetVibrance(int percent) => throw new NotSupportedException();
        public bool ResetVibrance() => throw new NotSupportedException();
        public void Suspend() => SuspendCalls++;
        public void Resume() => ResumeCalls++;
        public void Shutdown() => throw new NotSupportedException();
    }

    /// <summary>Gerçek parlaklık servisiyle: başta kaydırıcı ek karartma bölümünde (arka ışık 0, GPU karartıyor).</summary>
    private sealed class DimmedRig
    {
        public DimmedRig()
        {
            Monitor.SetMonitorValue(Preset, CustomRgb);
            Ddc = new DirectDdcControl(Monitor, Profile);
            Brightness_ = new BrightnessControl(Ddc, Pipeline, new DimmingPlanner());
            var panel = new DdcPanel(Profile, Ddc, BlueLight, Store, Key, ConnectionType.Hdmi, () => Brightness_.DimmingActive);
            Wizard = new SetupWizard(panel, Brightness_, BlueLight, Nvidia, Pipeline, new CalibrationCurves(), Store, Key, new FakeTimeProvider());

            Brightness_.Commit(0.1);
            Assert.True(Brightness_.DimmingActive);
            Assert.Equal(0, Monitor.Read(FakeHardware.Display, Brightness)!.Value.Current);
        }

        public FakeDdcMonitor Monitor { get; } = FakeDdcMonitor.HpP21bG4();
        public DirectDdcControl Ddc { get; }
        public RecordingPipeline Pipeline { get; } = new();
        public StubNvidia Nvidia { get; } = new();
        public BrightnessControl Brightness_ { get; }
        public StubBlueLight BlueLight { get; } = new();
        public FakeStore Store { get; } = new();
        public SetupWizard Wizard { get; }
    }

    private sealed class Rig
    {
        public Rig(FakeDdcMonitor? monitor = null, bool customRgb = true)
        {
            Monitor = monitor ?? FakeDdcMonitor.HpP21bG4();
            if (customRgb) Monitor.SetMonitorValue(Preset, CustomRgb);
            Ddc = new DirectDdcControl(Monitor, Profile);
            Wizard = New();
        }

        /// <summary>Aynı sahte donanım ve depoyla yeni bir sihirbaz (uygulama yeniden açıldı).</summary>
        public SetupWizard New() => new(
            new DdcPanel(Profile, Ddc, BlueLight, Store, Key, ConnectionType.Hdmi, () => Brightness_.DimmingActive),
            Brightness_, BlueLight, Nvidia, Pipeline, new CalibrationCurves(), Store, Key, Time);

        public FakeDdcMonitor Monitor { get; }
        public DirectDdcControl Ddc { get; }
        public StubBlueLight BlueLight { get; } = new();
        public StubBrightness Brightness_ { get; } = new();
        public StubNvidia Nvidia { get; } = new();
        public RecordingPipeline Pipeline { get; } = new();
        public FakeStore Store { get; } = new();
        public FakeTimeProvider Time { get; } = new();
        public SetupWizard Wizard { get; }

        public int Value(byte code) => Monitor.Read(FakeHardware.Display, code)!.Value.Current;
    }

    [Fact]
    public void Steps_AreInOrder_GammaAndGreyBalanceAfterWhiteMatch() =>
        Assert.Equal(
            [WizardStep.WarmUpAndPosition, WizardStep.BlackAndWhiteClipping, WizardStep.Sharpness, WizardStep.FullRgbCheck,
             WizardStep.ViewingAngle, WizardStep.DeadPixelsAndUniformity, WizardStep.WhiteMatch,
             WizardStep.Gamma, WizardStep.GreyBalance],
            new Rig().Wizard.Steps);

    /// <summary>Sihirbaz başlarken efektler kapanır, bitince geri gelir.</summary>
    [Fact]
    public void Begin_TurnsEffectsOff_FinishBringsThemBack()
    {
        var rig = new Rig();

        rig.Wizard.Begin();
        rig.Wizard.Begin(); // ikinci çağrı efektleri bir daha kapatmaz

        Assert.Equal(1, rig.BlueLight.SuspendCalls);
        Assert.Equal(1, rig.Brightness_.SuspendCalls);
        Assert.Equal(0, rig.BlueLight.ResumeCalls + rig.Brightness_.ResumeCalls);
        Assert.Empty(rig.Monitor.Writes); // Begin monitöre hiçbir şey yazmaz

        rig.Wizard.Finish();

        Assert.Equal(1, rig.BlueLight.ResumeCalls);
        Assert.Equal(1, rig.Brightness_.ResumeCalls);
        Assert.False(rig.Wizard.IsActive);
    }

    /// <summary>Bitince DDC değerleri ve kalibrasyon temel ayar olarak kaydedilir.</summary>
    [Fact]
    public void Finish_SavesBaseline()
    {
        var rig = new Rig();
        rig.Wizard.Begin();
        Assert.Equal(PanelSetResult.Written, rig.Wizard.SetDdc(PanelSetting.Brightness, 40));
        Assert.Equal(PanelSetResult.Written, rig.Wizard.SetDdc(PanelSetting.Contrast, 70));
        Assert.Equal(PanelSetResult.Written, rig.Wizard.SetDdc(PanelSetting.Sharpness, 3));
        Assert.Equal(PanelSetResult.Written, rig.Wizard.SetDdc(PanelSetting.RedGain, 250));
        Assert.Equal(PanelSetResult.Written, rig.Wizard.SetDdc(PanelSetting.BlueGain, 245));

        var baseline = rig.Wizard.Finish();

        Assert.Equal(40, baseline.DdcValues[Brightness]);
        Assert.Equal(70, baseline.DdcValues[Contrast]);
        Assert.Equal(3, baseline.DdcValues[Sharpness]);
        Assert.Equal((250, 255, 245), ((int)baseline.DdcValues[Red], (int)baseline.DdcValues[Green], (int)baseline.DdcValues[Blue]));
        Assert.DoesNotContain(Preset, baseline.DdcValues.Keys); // renk ön ayarı sihirbazın değil
        Assert.Equal(GammaRamp.Identity, baseline.Calibration); // kalibrasyon seçilmedi: birim tablo

        // Yeni örnek (uygulama yeniden açıldı) aynı temel ayarı depodan okur.
        var reopened = rig.New().LoadBaseline();
        Assert.NotNull(reopened);
        Assert.Equal(baseline.DdcValues, reopened.DdcValues);
        Assert.Equal(GammaRamp.Identity, reopened.Calibration);
    }

    /// <summary>
    /// Başta ek karartma açıktı (arka ışık 0). Sihirbaz arka ışığı 40 yaptıysa eski kaydırıcının GPU karartması geri
    /// gelmez: ikisi üst üste binip ekranı koyulaştırırdı. Kaydırıcı yeni arka ışıktan kurulur, arka ışık kilitlenmez.
    /// </summary>
    [Fact]
    public void Finish_AfterBacklightChange_DoesNotBringBackOldDimming()
    {
        var rig = new DimmedRig();
        rig.Wizard.Begin();
        Assert.Equal(PanelSetResult.Written, rig.Wizard.SetDdc(PanelSetting.Brightness, 40));

        rig.Wizard.Finish();

        Assert.False(rig.Brightness_.DimmingActive);
        Assert.Null(rig.Pipeline.Dimming); // GPU ölçeği 1: katman kapalı
        Assert.Equal(40, rig.Monitor.Read(FakeHardware.Display, Brightness)!.Value.Current);
        Assert.Equal(DimmingPlanner.SliderForBacklight(40), rig.Brightness_.ReadSlider());
    }

    /// <summary>Sihirbaz arka ışığa dokunmadıysa kullanıcının ek karartması geri gelir.</summary>
    [Fact]
    public void Finish_WithoutBacklightChange_BringsBackDimming()
    {
        var rig = new DimmedRig();
        var before = rig.Pipeline.Dimming;
        rig.Wizard.Begin();
        Assert.Null(rig.Pipeline.Dimming);
        rig.Wizard.SetDdc(PanelSetting.Contrast, 70);

        rig.Wizard.Finish();

        Assert.True(rig.Brightness_.DimmingActive);
        Assert.Equal(before, rig.Pipeline.Dimming);
    }

    /// <summary>İptal arka ışığı 0'a döndürür; ek karartma da eski kaydırıcıyla geri gelir.</summary>
    [Fact]
    public void Cancel_AfterBacklightChange_RestoresBacklightAndDimming()
    {
        var rig = new DimmedRig();
        var before = rig.Pipeline.Dimming;
        rig.Wizard.Begin();
        rig.Wizard.SetDdc(PanelSetting.Brightness, 40);

        Assert.Empty(rig.Wizard.Cancel());

        Assert.Equal(0, rig.Monitor.Read(FakeHardware.Display, Brightness)!.Value.Current);
        Assert.True(rig.Brightness_.DimmingActive);
        Assert.Equal(before, rig.Pipeline.Dimming);
    }

    [Fact]
    public void Cancel_RestoresValues_ResumesEffects_SavesNothing()
    {
        var rig = new Rig();
        rig.Wizard.Begin();
        rig.Wizard.SetDdc(PanelSetting.Brightness, 40);
        rig.Wizard.SetDdc(PanelSetting.Contrast, 60);
        rig.Wizard.SetDdc(PanelSetting.GreenGain, 240);

        var failed = rig.Wizard.Cancel();

        Assert.Empty(failed);
        Assert.Equal((55, 72, 255), (rig.Value(Brightness), rig.Value(Contrast), rig.Value(Green)));
        Assert.Equal(1, rig.BlueLight.ResumeCalls);
        Assert.Equal(1, rig.Brightness_.ResumeCalls);
        Assert.False(rig.Wizard.IsActive);
        Assert.DoesNotContain(SetupWizard.Section, rig.Store.Sections.Keys);
    }

    [Fact]
    public void Cancel_ReportsWhatCouldNotBeRestored_AndStillResumesEffects()
    {
        var rig = new Rig();
        rig.Wizard.Begin();
        rig.Wizard.SetDdc(PanelSetting.Brightness, 40);
        rig.Wizard.SetDdc(PanelSetting.Sharpness, 3);
        rig.Ddc.Override = code => code == Sharpness ? DdcWriteResult.Throttled : null;

        var failed = rig.Wizard.Cancel();

        Assert.Equal([PanelSetting.Sharpness], failed);
        Assert.Equal(55, rig.Value(Brightness));
        Assert.Equal(1, rig.BlueLight.ResumeCalls);
        Assert.Equal(1, rig.Brightness_.ResumeCalls);
    }

    /// <summary>Efektler kapandığı için mavi ışık ve ek karartma yüzünden kilitli ayarlar sihirbazda açılır.</summary>
    [Fact]
    public void SetDdc_IsNotLockedByBlueLightOrDimming_WhileWizardRuns()
    {
        var rig = new Rig();

        rig.Wizard.Begin();

        Assert.Equal(PanelSetResult.Written, rig.Wizard.SetDdc(PanelSetting.Brightness, 40));
        Assert.Equal(PanelSetResult.Written, rig.Wizard.SetDdc(PanelSetting.GreenGain, 250));
    }

    [Fact]
    public void SetDdc_KeepsDigitalBrightnessProtection()
    {
        var rig = new Rig();
        rig.Wizard.Begin();

        Assert.Equal(PanelSetResult.BlockedDigitalBoost, rig.Wizard.SetDdc(PanelSetting.Contrast, 90)); // tavan 72
        Assert.Equal(PanelSetResult.OutOfRange, rig.Wizard.SetDdc(PanelSetting.Contrast, 5)); // %15 tabanı
        Assert.Equal(0, rig.Monitor.WriteCount(Contrast));
    }

    [Fact]
    public void SetDdc_RgbGain_NeedsCustomRgbPreset()
    {
        var rig = new Rig(customRgb: false);
        rig.Wizard.Begin();

        Assert.Equal(PanelSetResult.Locked, rig.Wizard.SetDdc(PanelSetting.RedGain, 250));
        Assert.Equal(0, rig.Monitor.WriteCount(Red));
    }

    [Theory]
    [InlineData(PanelSetting.ColorPreset)] // Custom RGB profilde "estimated": ön ayarı kullanıcı OSD'den seçer
    [InlineData(PanelSetting.Input)]
    [InlineData(PanelSetting.Volume)]
    public void SetDdc_OnlyTheWizardsSettings(PanelSetting setting)
    {
        var rig = new Rig();
        rig.Wizard.Begin();

        Assert.Equal(PanelSetResult.NotSupported, rig.Wizard.SetDdc(setting, 10));
        Assert.Empty(rig.Monitor.Writes);
    }

    [Fact]
    public void WithoutDdc_StepsReportNoDdc_AndFinishStillWorks()
    {
        var rig = new Rig(FakeDdcMonitor.WithoutDdc(), customRgb: false);
        rig.Wizard.Begin();

        Assert.Equal(PanelSetResult.NoDdc, rig.Wizard.SetDdc(PanelSetting.Brightness, 40));
        var baseline = rig.Wizard.Finish();

        Assert.Empty(baseline.DdcValues);
        Assert.Equal(1, rig.Brightness_.ResumeCalls);
    }

    [Fact]
    public void BeforeBegin_SetDdcAndFinishRefuse()
    {
        var wizard = new Rig().Wizard;

        Assert.Throws<InvalidOperationException>(() => wizard.SetDdc(PanelSetting.Brightness, 40));
        Assert.Throws<InvalidOperationException>(wizard.Finish);
        Assert.Empty(wizard.Cancel()); // Begin'den önce iptal zararsız
    }

    [Fact]
    public void WarmUp_CountsDownThirtyMinutes_FromBegin()
    {
        var rig = new Rig();
        Assert.Equal(TimeSpan.FromMinutes(30), SetupWizard.WarmUp);
        rig.Wizard.Begin();

        Assert.Equal(SetupWizard.WarmUp, rig.Wizard.WarmUpRemaining);
        rig.Time.Advance(TimeSpan.FromMinutes(12));
        Assert.Equal(TimeSpan.FromMinutes(18), rig.Wizard.WarmUpRemaining);
        rig.Time.Advance(TimeSpan.FromMinutes(40));
        Assert.Equal(TimeSpan.Zero, rig.Wizard.WarmUpRemaining);
    }

    private static CalibrationChoices Choices(RgbGains? white = null) =>
        new(2.6, RgbGains.Neutral, new RgbGains(0.92, 1, 1), RgbGains.Neutral, white);

    private static GammaRamp Expected(CalibrationChoices choices) => new CalibrationCurves().Build(choices);

    /// <summary>Seçimler anında ekrana (kalibrasyon katmanı) gider, bitince temel ayarda saklanır, yeniden açılışta okunur.</summary>
    [Fact]
    public void SetCalibration_AppliesTheCurveLive_FinishSavesItAsBaseline()
    {
        var rig = new Rig();
        rig.Wizard.Begin();

        Assert.True(rig.Wizard.SetCalibration(Choices()));

        Assert.Equal(Expected(Choices()), rig.Pipeline.Calibration);
        Assert.Equal(1, rig.Pipeline.Applies);
        Assert.Empty(rig.Monitor.Writes); // GPU yolu: monitöre (DDC) dokunmaz

        var baseline = rig.Wizard.Finish();

        Assert.Equal(Expected(Choices()), baseline.Calibration);
        Assert.Equal(Expected(Choices()), rig.Pipeline.Calibration); // katman yerinde kalır
        Assert.Equal(Expected(Choices()), rig.New().LoadBaseline()!.Calibration);
    }

    [Fact]
    public void SetCalibration_BeforeBegin_Refuses_AndInvalidChoicesApplyNothing()
    {
        var rig = new Rig();
        Assert.Throws<InvalidOperationException>(() => rig.Wizard.SetCalibration(Choices()));

        rig.Wizard.Begin();
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.Wizard.SetCalibration(Choices() with { PanelGamma = 3.5 }));
        Assert.Equal(0, rig.Pipeline.Applies);
        Assert.Null(rig.Pipeline.Calibration);
    }

    /// <summary>Windows tabloyu reddederse ekrandaki ve kaydedilecek kalibrasyon önceki hâlinde kalır.</summary>
    [Fact]
    public void SetCalibration_WhenWindowsRejectsTheTable_ReturnsFalse_AndKeepsThePreviousOne()
    {
        var rig = new Rig();
        rig.Wizard.Begin();
        Assert.True(rig.Wizard.SetCalibration(Choices()));

        rig.Pipeline.ApplyResult = false;
        Assert.False(rig.Wizard.SetCalibration(Choices() with { PanelGamma = 2.0 }));
        rig.Pipeline.ApplyResult = true;

        Assert.Equal(Expected(Choices()), rig.Pipeline.Calibration);
        Assert.Equal(Expected(Choices()), rig.Wizard.Finish().Calibration);
    }

    /// <summary>
    /// Eski kalibrasyon açıkken panelin gammasını ölçmek anlamsız (düzeltilmiş ekrana bakılır): Begin ham panele döner,
    /// seçim yapılmazsa Finish eskisini geri koyar ve kaydı korur.
    /// </summary>
    [Fact]
    public void Begin_ShowsTheRawPanel_FinishWithoutChoiceBringsTheOldCalibrationBack()
    {
        var rig = new Rig();
        rig.Wizard.Begin();
        rig.Wizard.SetCalibration(Choices());
        rig.Wizard.Finish();

        var again = rig.New();
        again.Begin();
        Assert.Null(rig.Pipeline.Calibration); // ham panel
        var baseline = again.Finish();

        Assert.Equal(Expected(Choices()), rig.Pipeline.Calibration);
        Assert.Equal(Expected(Choices()), baseline.Calibration);
    }

    [Fact]
    public void Cancel_RestoresThePreviousCalibration()
    {
        var rig = new Rig();
        rig.Wizard.Begin();
        rig.Wizard.SetCalibration(Choices());
        rig.Wizard.Finish();

        var again = rig.New();
        again.Begin();
        again.SetCalibration(Choices() with { PanelGamma = 2.0 });
        Assert.Empty(again.Cancel());

        Assert.Equal(Expected(Choices()), rig.Pipeline.Calibration);
        Assert.Equal(Expected(Choices()), rig.New().LoadBaseline()!.Calibration); // kayıt değişmedi
    }

    [Fact]
    public void Cancel_WithoutAnyPreviousCalibration_ClearsTheLayer()
    {
        var rig = new Rig();
        rig.Wizard.Begin();
        rig.Wizard.SetCalibration(Choices());

        rig.Wizard.Cancel();

        Assert.Null(rig.Pipeline.Calibration);
        Assert.Null(rig.Wizard.LoadBaseline());
    }

    /// <summary>Sihirbaz kalibrasyona hiç dokunmadıysa boru hattına yazmaz.</summary>
    [Fact]
    public void WithoutCalibration_PipelineIsNotTouched()
    {
        var rig = new Rig();
        rig.Wizard.Begin();
        rig.Wizard.Finish();
        rig.Wizard.Begin();
        rig.Wizard.Cancel();

        Assert.Equal(0, rig.Pipeline.Applies);
    }

    /// <summary>DDC yoksa beyaz eşleme GPU'da: kazanç kalibrasyon katmanında beyaz kazancı olarak gider.</summary>
    [Fact]
    public void WithoutDdc_WhiteMatchRunsOnTheGpu_ThroughTheCalibrationLayer()
    {
        var rig = new Rig(FakeDdcMonitor.WithoutDdc(), customRgb: false);
        rig.Wizard.Begin();
        Assert.Equal(PanelSetResult.NoDdc, rig.Wizard.SetDdc(PanelSetting.BlueGain, 240));
        var white = new RgbGains(1, 0.97, 0.9);

        Assert.True(rig.Wizard.SetCalibration(Choices(white)));
        var baseline = rig.Wizard.Finish();

        Assert.Equal(GammaRamp.Identity.Red[255], rig.Pipeline.Calibration!.Red[255]);
        Assert.True(rig.Pipeline.Calibration.Blue[255] < 65535); // mavi kısıldı
        Assert.Equal(Expected(Choices(white)), baseline.Calibration);
        Assert.Empty(baseline.DdcValues);
    }

    /// <summary>Canlılık (T303) sihirbazda kapalı: Begin kapatır, bitince ya da iptalde geri gelir.</summary>
    [Fact]
    public void Vibrance_IsSuspendedWhileWizardRuns_AndResumedOnFinishAndCancel()
    {
        var rig = new Rig();
        rig.Wizard.Begin();
        rig.Wizard.Begin();
        Assert.Equal((1, 0), (rig.Nvidia.SuspendCalls, rig.Nvidia.ResumeCalls));

        rig.Wizard.Finish();
        Assert.Equal((1, 1), (rig.Nvidia.SuspendCalls, rig.Nvidia.ResumeCalls));

        rig.Wizard.Begin();
        rig.Wizard.Cancel();
        Assert.Equal((2, 2), (rig.Nvidia.SuspendCalls, rig.Nvidia.ResumeCalls));
    }
}
