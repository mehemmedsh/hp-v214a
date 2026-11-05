using MonitorTuner.App.ViewModels;
using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Services;

namespace MonitorTuner.Windows.Tests.ViewModels;

/// <summary>
/// Ayar sihirbazı sayfası (T315): başlatma, adımlar arasında gezinme, desen açma, DDC değerleri (sadece sihirbaz üzerinden),
/// gamma ve gri dengesi seçimleri, DDC yoksa GPU'da beyaz eşleme, bitiş ve iptal. Donanım yok: sahte sihirbaz ve panel.
/// </summary>
public class SetupWizardViewModelTests
{
    private sealed class StubWizard : ISetupWizard
    {
        public IReadOnlyList<WizardStep> Steps { get; } =
        [
            WizardStep.WarmUpAndPosition, WizardStep.BlackAndWhiteClipping, WizardStep.Sharpness, WizardStep.FullRgbCheck,
            WizardStep.ViewingAngle, WizardStep.DeadPixelsAndUniformity, WizardStep.WhiteMatch, WizardStep.Gamma, WizardStep.GreyBalance,
        ];

        public bool IsActive { get; set; }
        public TimeSpan WarmUpRemaining { get; set; } = TimeSpan.FromMinutes(30);
        public List<string> Calls { get; } = [];
        public List<(PanelSetting Setting, int Value)> DdcSets { get; } = [];
        public List<CalibrationChoices> Calibrations { get; } = [];
        public PanelSetResult DdcResult { get; set; } = PanelSetResult.Written;
        public bool CalibrationResult { get; set; } = true;
        public IReadOnlyList<PanelSetting> CancelFailures { get; set; } = [];

        public void Begin() { Calls.Add("begin"); IsActive = true; }

        public PanelSetResult SetDdc(PanelSetting setting, int value)
        {
            Assert.True(IsActive, "Sihirbaz kapalıyken yazma");
            DdcSets.Add((setting, value));
            return DdcResult;
        }

        public bool SetCalibration(CalibrationChoices choices)
        {
            Assert.True(IsActive, "Sihirbaz kapalıyken kalibrasyon");
            Calibrations.Add(choices);
            return CalibrationResult;
        }

        public Baseline Finish() { Calls.Add("finish"); IsActive = false; return new Baseline(new Dictionary<byte, ushort>(), GammaRamp.Identity); }

        public IReadOnlyList<PanelSetting> Cancel() { Calls.Add("cancel"); IsActive = false; return CancelFailures; }

        public Baseline? LoadBaseline() => null;
    }

    private sealed class StubPanel : IDdcPanel
    {
        public bool Available { get; set; } = true;
        public List<(PanelSetting Setting, int Value)> Sets { get; } = [];

        public DdcPanelState Refresh() => !Available
            ? new DdcPanelState(false, [], [])
            : new DdcPanelState(true,
            [
                Slider(PanelSetting.Brightness, 0x10, 70), Slider(PanelSetting.Contrast, 0x12, 80),
                Slider(PanelSetting.RedGain, 0x16, 100), Slider(PanelSetting.GreenGain, 0x18, 100), Slider(PanelSetting.BlueGain, 0x1A, 100),
                Slider(PanelSetting.Sharpness, 0x87, 4, max: 8), Slider(PanelSetting.Volume, 0x62, 20),
            ], []);

        public PanelSetResult Set(PanelSetting setting, int value, bool inputWarningAccepted = false)
        {
            Sets.Add((setting, value));
            return PanelSetResult.Written;
        }

        private static PanelSettingState Slider(PanelSetting s, byte code, int current, int max = 100) =>
            new(s, code, current, 0, max, max, SettingLock.None, []);
    }

    private static (SetupWizardViewModel Vm, StubWizard Wizard, StubPanel Panel) Started(bool ddc = true)
    {
        var wizard = new StubWizard();
        var panel = new StubPanel { Available = ddc };
        var vm = new SetupWizardViewModel(wizard, panel);
        vm.Start();
        return (vm, wizard, panel);
    }

    private static void GoTo(SetupWizardViewModel vm, WizardStep step)
    {
        while (vm.CurrentStep != step) vm.Next();
    }

    [Fact]
    public void BeforeStart_NothingIsWritten_AndStartBeginsTheWizardAtWarmUp()
    {
        var wizard = new StubWizard();
        var vm = new SetupWizardViewModel(wizard, new StubPanel());

        Assert.False(vm.IsActive);
        Assert.True(vm.IsIdle);
        Assert.Empty(wizard.Calls);

        vm.Start();

        Assert.Equal(["begin"], wizard.Calls);
        Assert.True(vm.IsActive);
        Assert.Equal(WizardStep.WarmUpAndPosition, vm.CurrentStep);
        Assert.Equal("Isınma ve konum", vm.StepTitle);
        Assert.Equal("Adım 1 / 9", vm.StepCounter);
        Assert.True(vm.ShowWarmUp);
        Assert.Contains("30", vm.WarmUpText);
        Assert.Empty(wizard.DdcSets);
        Assert.Empty(wizard.Calibrations);
    }

    [Fact]
    public void WarmUpCountdown_FollowsTheWizard_AndSaysWhenDone()
    {
        var (vm, wizard, _) = Started();
        wizard.WarmUpRemaining = TimeSpan.FromMinutes(12.2);
        vm.RefreshWarmUp();
        Assert.Contains("13", vm.WarmUpText); // yukarı yuvarlanır: "0 dk kaldı" hiç görünmez

        wizard.WarmUpRemaining = TimeSpan.Zero;
        vm.RefreshWarmUp();
        Assert.DoesNotContain("kaldı", vm.WarmUpText);

        vm.Next();
        Assert.False(vm.ShowWarmUp);
    }

    [Fact]
    public void NextAndBack_WalkEveryStepInOrder_AndStopAtBothEnds()
    {
        var (vm, wizard, _) = Started();

        Assert.False(vm.CanGoBack);
        vm.Back();
        Assert.Equal(0, vm.StepIndex);

        for (var i = 0; i < wizard.Steps.Count; i++)
        {
            Assert.Equal(wizard.Steps[i], vm.CurrentStep);
            Assert.DoesNotContain("[[", vm.StepTitle);
            Assert.DoesNotContain("[[", vm.StepText);
            Assert.Equal($"Adım {i + 1} / 9", vm.StepCounter);
            Assert.Equal(i == wizard.Steps.Count - 1, vm.IsLastStep);
            vm.Next();
        }

        Assert.Equal(WizardStep.GreyBalance, vm.CurrentStep); // son adımda durur
        Assert.False(vm.CanGoNext);
        vm.Back();
        Assert.Equal(WizardStep.Gamma, vm.CurrentStep);
        Assert.Equal(["begin"], wizard.Calls); // gezinme hiçbir şey yazmaz
    }

    [Fact]
    public void EveryStep_OffersItsPatterns_WithTitles_AndOpenPatternShowsIt()
    {
        var (vm, wizard, _) = Started();
        var shown = new List<Pattern>();
        vm.ShowPattern = p => { shown.Add(p); return 0; };

        foreach (var step in wizard.Steps)
        {
            GoTo(vm, step);
            Assert.Equal(WizardPatterns.For(step).Select(p => p.Kind), vm.Patterns.Select(p => p.Pattern.Kind));
            Assert.Equal(vm.Patterns.Count > 0, vm.HasPatterns);
            foreach (var item in vm.Patterns)
            {
                Assert.DoesNotContain("[[", item.Title);
                vm.OpenPattern(item);
            }
        }

        Assert.Equal(Enum.GetValues<WizardStep>().Sum(s => WizardPatterns.For(s).Count), shown.Count);
        Assert.Empty(wizard.Calibrations); // desen açmak kalibrasyon seçmek değildir
    }

    [Fact]
    public void DdcSliders_BelongToTheirStep_AndReleaseWritesOnceThroughTheWizard()
    {
        var (vm, wizard, panel) = Started();

        GoTo(vm, WizardStep.BlackAndWhiteClipping);
        Assert.Equal([PanelSetting.Contrast, PanelSetting.Brightness], vm.DdcItems.Select(i => i.Setting));
        Assert.True(vm.ShowDdcItems);

        var contrast = vm.DdcItems[0];
        contrast.Value = 75; contrast.Value = 72; // sürükleme yazmaz
        Assert.Empty(wizard.DdcSets);

        vm.CommitDdc(contrast);

        Assert.Equal([(PanelSetting.Contrast, 72)], wizard.DdcSets);
        Assert.Empty(panel.Sets); // panelden doğrudan değil: iptal sihirbazın yazdığını geri alabilsin
        Assert.Equal("Ayar yazıldı.", vm.Status);

        GoTo(vm, WizardStep.Sharpness);
        Assert.Equal([PanelSetting.Sharpness], vm.DdcItems.Select(i => i.Setting));
        GoTo(vm, WizardStep.FullRgbCheck);
        Assert.Empty(vm.DdcItems);
        Assert.False(vm.ShowDdcItems);
        GoTo(vm, WizardStep.WhiteMatch);
        Assert.Equal([PanelSetting.RedGain, PanelSetting.GreenGain, PanelSetting.BlueGain], vm.DdcItems.Select(i => i.Setting));
        Assert.False(vm.ShowGpuWhite);
    }

    [Fact]
    public void RefusedDdcWrite_IsExplained()
    {
        var (vm, wizard, _) = Started();
        GoTo(vm, WizardStep.BlackAndWhiteClipping);
        wizard.DdcResult = PanelSetResult.BlockedDigitalBoost;

        vm.CommitDdc(vm.DdcItems[0]);

        Assert.Contains("arka ışığı", vm.Status);
    }

    [Fact]
    public void WithoutDdc_StepsSayUseOsd_AndWhiteMatchUsesGpuGains()
    {
        var (vm, wizard, _) = Started(ddc: false);

        GoTo(vm, WizardStep.BlackAndWhiteClipping);
        Assert.True(vm.ShowNoDdc);
        Assert.Empty(vm.DdcItems);

        GoTo(vm, WizardStep.WhiteMatch);
        Assert.True(vm.ShowGpuWhite);
        Assert.Equal(3, vm.GpuWhiteItems.Count);
        Assert.All(vm.GpuWhiteItems, g => Assert.Equal((80, 100, 100), (g.Minimum, g.Maximum, g.Value)));

        vm.GpuWhiteItems[2].Value = 92; // mavi
        Assert.Empty(wizard.Calibrations); // sürükleme yazmaz
        vm.CommitGain(vm.GpuWhiteItems[2]);

        var choice = Assert.Single(wizard.Calibrations);
        Assert.Equal(new RgbGains(1, 1, 0.92), choice.WhiteGains);
        Assert.Equal(CalibrationChoices.TargetGamma, choice.PanelGamma); // gamma seçilmedi: düzeltme yok
    }

    [Fact]
    public void Gamma_OffersEveryCandidate_AndSelectingAppliesIt()
    {
        var (vm, wizard, _) = Started();
        GoTo(vm, WizardStep.Gamma);

        Assert.True(vm.IsGammaStep);
        Assert.Equal(WizardPatterns.GammaCandidates, vm.GammaChoices.Select(c => c.Gamma));
        Assert.Null(vm.SelectedGamma);

        vm.SelectedGamma = vm.GammaChoices.Single(c => c.Gamma == 2.4);

        var choice = Assert.Single(wizard.Calibrations);
        Assert.Equal(2.4, choice.PanelGamma);
        Assert.Equal(RgbGains.Neutral, choice.MidGrey);
        Assert.Null(choice.WhiteGains); // DDC var: beyaz eşleme monitörde
    }

    [Fact]
    public void ClosingTheGammaPattern_TellsWhichFrameWasLastSeen_ButSelectsNothing()
    {
        var (vm, wizard, _) = Started();
        GoTo(vm, WizardStep.Gamma);
        var index = WizardPatterns.GammaCandidates.ToList().IndexOf(2.3);
        vm.ShowPattern = _ => index;

        vm.OpenPattern(vm.Patterns.Single());

        Assert.Contains(vm.GammaChoices[index].Title, vm.Status);
        Assert.Null(vm.SelectedGamma);
        Assert.Empty(wizard.Calibrations);
    }

    [Fact]
    public void GreyBalance_HasThreeLevelsTimesThreeChannels_AndReleaseRebuildsCalibration()
    {
        var (vm, wizard, _) = Started();
        GoTo(vm, WizardStep.Gamma);
        vm.SelectedGamma = vm.GammaChoices.Single(c => c.Gamma == 2.0);
        GoTo(vm, WizardStep.GreyBalance);

        Assert.True(vm.IsGreyBalanceStep);
        Assert.Equal(9, vm.GreyItems.Count);
        Assert.All(vm.GreyItems, g => Assert.Equal((80, 125, 100), (g.Minimum, g.Maximum, g.Value)));
        Assert.All(vm.GreyItems, g => Assert.DoesNotContain("[[", g.Title));

        var midRed = vm.GreyItems[3]; // sıra: koyu R G B, orta R G B, açık R G B
        midRed.Value = 95;
        vm.CommitGain(midRed);

        var last = wizard.Calibrations[^1];
        Assert.Equal(2.0, last.PanelGamma); // önceki gamma seçimi korunur
        Assert.Equal(new RgbGains(0.95, 1, 1), last.MidGrey);
        Assert.Equal(RgbGains.Neutral, last.DarkGrey);
        Assert.Equal(RgbGains.Neutral, last.LightGrey);
    }

    [Fact]
    public void RejectedCalibration_IsExplained()
    {
        var (vm, wizard, _) = Started();
        GoTo(vm, WizardStep.Gamma);
        wizard.CalibrationResult = false;

        vm.SelectedGamma = vm.GammaChoices[0];

        Assert.Contains("Windows", vm.Status);
    }

    [Fact]
    public void Finish_SavesAndReturnsToIdle_ThenAFreshStartBeginsAgainCleanly()
    {
        var (vm, wizard, _) = Started();
        GoTo(vm, WizardStep.Gamma);
        vm.SelectedGamma = vm.GammaChoices[2];

        vm.Finish();

        Assert.Equal(["begin", "finish"], wizard.Calls);
        Assert.False(vm.IsActive);
        Assert.Contains("kaydedildi", vm.Status);

        vm.Start();
        Assert.Equal(WizardStep.WarmUpAndPosition, vm.CurrentStep);
        GoTo(vm, WizardStep.Gamma);
        Assert.Null(vm.SelectedGamma); // eski seçim yeni turda kalmaz
    }

    [Fact]
    public void Cancel_ReturnsToIdle_AndListsSettingsThatCouldNotBeRestored()
    {
        var (vm, wizard, _) = Started();
        wizard.CancelFailures = [PanelSetting.Contrast];

        vm.Cancel();

        Assert.Equal(["begin", "cancel"], wizard.Calls);
        Assert.False(vm.IsActive);
        Assert.Contains("Kontrast", vm.Status);

        var (vm2, wizard2, _) = Started();
        vm2.Cancel();
        Assert.DoesNotContain("Kontrast", vm2.Status);
        Assert.Equal(["begin", "cancel"], wizard2.Calls);
    }

    [Fact]
    public void ViewModelCreatedWhileWizardIsActive_ShowsTheActiveWizard()
    {
        var wizard = new StubWizard { IsActive = true };

        var vm = new SetupWizardViewModel(wizard, new StubPanel());

        Assert.True(vm.IsActive);
        vm.Start(); // ikinci Begin yok
        Assert.Empty(wizard.Calls);
    }

    [Fact]
    public void Start_TwiceWhileTheLaneIsBusy_BeginsOnce_AndButtonsAreOffMeanwhile()
    {
        var lane = new ManualLane();
        var wizard = new StubWizard();
        var vm = new SetupWizardViewModel(wizard, new StubPanel(), lane);

        var first = vm.Start();
        vm.Start(); // çift tıklama

        Assert.True(vm.IsBusy);
        Assert.False(vm.IsReady);
        Assert.False(vm.IsActive);
        lane.RunAll();
        Assert.True(first.IsCompletedSuccessfully);
        Assert.Equal(["begin"], wizard.Calls);
        Assert.True(vm.IsActive);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public void Next_LateDdcReadOfThePreviousStep_DoesNotFillTheNewStep()
    {
        var lane = new ManualLane();
        var vm = new SetupWizardViewModel(new StubWizard(), new StubPanel(), lane);
        vm.Start();
        lane.RunAll();

        vm.Next(); // siyah/beyaz kırpma: kontrast ve parlaklık okunacak
        vm.Next(); // okuma gelmeden keskinliğe geçildi
        lane.RunAll();

        Assert.Equal(WizardStep.Sharpness, vm.CurrentStep);
        Assert.Equal([PanelSetting.Sharpness], vm.DdcItems.Select(i => i.Setting));
        Assert.False(vm.IsBusy);
    }
}
