using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Services;

namespace MonitorTuner.Tests.Services;

/// <summary>
/// Hızlı denetimler (T310): tepsinin ve ana pencerenin ortak giriş noktası. Profil seçimi, parlaklık ve mavi ışık sihirbaz
/// açıkken kapalıdır; panik her zaman çalışır ve sadece <see cref="IColorProfiles.PanicOff"/> çağırır. Sahte servislerle.
/// </summary>
public class QuickControlsTests
{
    private sealed class FakeProfiles(List<string> log) : IColorProfiles
    {
        public List<ColorProfile> List { get; } = [.. ColorProfiles.BuiltIns, new("Benim", false, 0.4, 4500, null)];
        public ProfileApplyResult? Result { get; set; }
        public Exception? PanicError { get; set; }

        public IReadOnlyList<ColorProfile> Profiles => List;

        public ProfileApplyResult Apply(ColorProfile profile)
        {
            log.Add("apply:" + profile.Name);
            return Result ?? new ProfileApplyResult(profile.Name, ProfileStep.Applied, ProfileStep.Applied, ProfileStep.Skipped);
        }

        public void PanicOff()
        {
            log.Add("panic");
            if (PanicError is not null) throw PanicError;
        }

        public void Save(ColorProfile profile) => throw new NotSupportedException();
        public void Delete(string name) => throw new NotSupportedException();
    }

    private sealed class FakeBrightness(List<string> log) : IBrightnessControl
    {
        public bool Applied { get; set; } = true;
        public bool DimmingActive => false;
        public DimmingPlan Preview(double slider) => throw new NotSupportedException();

        public BrightnessState Commit(double slider)
        {
            log.Add($"brightness:{slider.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
            return new BrightnessState(new DimmingPlan(0, 1), Applied, null);
        }

        public double? ReadSlider() => throw new NotSupportedException();
        public bool DisableDimming() => throw new NotSupportedException();
        public bool Suspend() => throw new NotSupportedException();
        public bool Resume() => throw new NotSupportedException();
        public bool ResumeAt(double slider) => throw new NotSupportedException();
        public bool Shutdown() => throw new NotSupportedException();
    }

    private sealed class FakeBlueLight(List<string> log) : IBlueLightFilter
    {
        public bool Applied { get; set; } = true;
        public int Kelvin { get; private set; } = 6500;
        public bool IsSuspended => false;
        public EveningSettings Evening { get; set; } = EveningSettings.Default;

        public BlueLightState SetKelvin(int kelvin)
        {
            log.Add($"kelvin:{kelvin}");
            Kelvin = kelvin;
            return new BlueLightState(kelvin, Applied, RgbGains.Neutral, RgbGains.Neutral, []);
        }

        public BlueLightState Disable() => throw new NotSupportedException();
        public BlueLightState Suspend() => throw new NotSupportedException();
        public BlueLightState Resume() => throw new NotSupportedException();
        public BlueLightState Shutdown() => throw new NotSupportedException();
        public LowBlueLightResult SelectMonitorLowBlueLight() => throw new NotSupportedException();
        public IReadOnlyList<ColorConflict> CheckConflicts() => throw new NotSupportedException();
        public bool Tick() => throw new NotSupportedException();
    }

    private sealed class FakeWizard : ISetupWizard
    {
        public bool Active { get; set; }
        public bool IsActive => Active;
        public IReadOnlyList<WizardStep> Steps => throw new NotSupportedException();
        public TimeSpan WarmUpRemaining => throw new NotSupportedException();
        public void Begin() => throw new NotSupportedException();
        public PanelSetResult SetDdc(PanelSetting setting, int value) => throw new NotSupportedException();
        public bool SetCalibration(CalibrationChoices choices) => throw new NotSupportedException();
        public Baseline Finish() => throw new NotSupportedException();
        public IReadOnlyList<PanelSetting> Cancel() => throw new NotSupportedException();
        public Baseline? LoadBaseline() => throw new NotSupportedException();
    }

    private sealed class Setup
    {
        public List<string> Log { get; } = [];
        public FakeProfiles Profiles { get; }
        public FakeBrightness Brightness { get; }
        public FakeBlueLight BlueLight { get; }
        public FakeWizard Wizard { get; } = new();
        public QuickControls Quick { get; }

        public Setup()
        {
            Profiles = new FakeProfiles(Log);
            Brightness = new FakeBrightness(Log);
            BlueLight = new FakeBlueLight(Log);
            Quick = new QuickControls(Profiles, Brightness, BlueLight, Wizard);
        }
    }

    // ---- Etkin mi ----

    [Fact]
    public void Enabled_IsTrue_UntilWizardIsActive()
    {
        var s = new Setup();
        Assert.True(s.Quick.Enabled);

        s.Wizard.Active = true;
        Assert.False(s.Quick.Enabled);

        s.Wizard.Active = false;
        Assert.True(s.Quick.Enabled);
    }

    [Fact]
    public void Profiles_ListsBuiltInAndUserProfiles()
    {
        var s = new Setup();

        Assert.Equal([.. ColorProfiles.BuiltIns.Select(p => p.Name), "Benim"],
            s.Quick.Profiles.Select(p => p.Name));
    }

    // ---- Profil seçimi ----

    [Fact]
    public void SelectProfile_AppliesTheNamedProfileOnce()
    {
        var s = new Setup();

        var result = s.Quick.SelectProfile("Film");

        Assert.Equal(QuickOutcome.Done, result.Outcome);
        Assert.Equal(["apply:Film"], s.Log);
        Assert.Equal("Film", result.Profile!.Name);
    }

    [Fact]
    public void SelectProfile_FindsUserProfile_IgnoringCase()
    {
        var s = new Setup();

        Assert.Equal(QuickOutcome.Done, s.Quick.SelectProfile("benim").Outcome);
        Assert.Equal(["apply:Benim"], s.Log);
    }

    [Fact]
    public void SelectProfile_UnknownName_WritesNothing()
    {
        var s = new Setup();

        var result = s.Quick.SelectProfile("Yok");

        Assert.Equal(QuickOutcome.NotFound, result.Outcome);
        Assert.Empty(s.Log);
    }

    [Fact]
    public void SelectProfile_FailedStep_IsPartial_AndReportsEachStep()
    {
        var s = new Setup();
        s.Profiles.Result = new ProfileApplyResult("Film", ProfileStep.Applied, ProfileStep.Failed, ProfileStep.Applied);

        var result = s.Quick.SelectProfile("Film");

        Assert.Equal(QuickOutcome.Partial, result.Outcome);
        Assert.Equal(ProfileStep.Failed, result.Profile!.BlueLight);
    }

    [Fact]
    public void SelectProfile_DeferredStep_IsNotReportedAsDone()
    {
        // Sihirbaz sırasında canlılık "Applied" görünür (istek saklanır) ama ekranda değildir; "Deferred" da tam başarı sayılmaz.
        var s = new Setup();
        s.Profiles.Result = new ProfileApplyResult("Akşam", ProfileStep.Applied, ProfileStep.Deferred, ProfileStep.Skipped);

        Assert.Equal(QuickOutcome.Partial, s.Quick.SelectProfile("Akşam").Outcome);
    }

    /// <summary>Ton eğrisi (T320) ertelendiyse ya da başarısızsa da "uygulandı" denmez.</summary>
    [Theory]
    [InlineData(ProfileStep.Deferred)]
    [InlineData(ProfileStep.Failed)]
    public void SelectProfile_ToneNotOnScreen_IsPartial(ProfileStep tone)
    {
        var s = new Setup();
        s.Profiles.Result = new ProfileApplyResult("Film", ProfileStep.Applied, ProfileStep.Applied, ProfileStep.Applied, tone);

        Assert.Equal(QuickOutcome.Partial, s.Quick.SelectProfile("Film").Outcome);
    }

    [Fact]
    public void SelectProfile_WhileWizardIsActive_IsBlockedAndWritesNothing()
    {
        var s = new Setup();
        s.Wizard.Active = true;

        var result = s.Quick.SelectProfile("Film");

        Assert.Equal(QuickOutcome.Blocked, result.Outcome);
        Assert.Null(result.Profile);
        Assert.Empty(s.Log);
    }

    // ---- Parlaklık ----

    [Theory]
    [InlineData(1.0, "brightness:1")]
    [InlineData(0.6, "brightness:0.6")]
    [InlineData(0.0, "brightness:0")]
    public void SetBrightness_CommitsOnce(double slider, string expected)
    {
        var s = new Setup();

        Assert.Equal(QuickOutcome.Done, s.Quick.SetBrightness(slider).Outcome);
        Assert.Equal([expected], s.Log);
    }

    [Fact]
    public void SetBrightness_NotApplied_IsFailed()
    {
        var s = new Setup();
        s.Brightness.Applied = false;

        Assert.Equal(QuickOutcome.Failed, s.Quick.SetBrightness(0.5).Outcome);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void SetBrightness_OutOfRange_ThrowsAndWritesNothing(double slider)
    {
        var s = new Setup();

        Assert.Throws<ArgumentOutOfRangeException>(() => s.Quick.SetBrightness(slider));
        Assert.Empty(s.Log);
    }

    [Fact]
    public void SetBrightness_WhileWizardIsActive_IsBlocked()
    {
        var s = new Setup();
        s.Wizard.Active = true;

        Assert.Equal(QuickOutcome.Blocked, s.Quick.SetBrightness(0.5).Outcome);
        Assert.Empty(s.Log);
    }

    // ---- Mavi ışık ----

    [Theory]
    [InlineData(6500)]
    [InlineData(3400)]
    [InlineData(1900)]
    public void SetBlueLight_SetsKelvinOnce(int kelvin)
    {
        var s = new Setup();

        Assert.Equal(QuickOutcome.Done, s.Quick.SetBlueLight(kelvin).Outcome);
        Assert.Equal([$"kelvin:{kelvin}"], s.Log);
    }

    [Fact]
    public void SetBlueLight_NotApplied_IsFailed()
    {
        var s = new Setup();
        s.BlueLight.Applied = false;

        Assert.Equal(QuickOutcome.Failed, s.Quick.SetBlueLight(3400).Outcome);
    }

    [Theory]
    [InlineData(1899)]
    [InlineData(6501)]
    public void SetBlueLight_OutOfRange_ThrowsAndWritesNothing(int kelvin)
    {
        var s = new Setup();

        Assert.Throws<ArgumentOutOfRangeException>(() => s.Quick.SetBlueLight(kelvin));
        Assert.Empty(s.Log);
    }

    [Fact]
    public void SetBlueLight_WhileWizardIsActive_IsBlocked()
    {
        var s = new Setup();
        s.Wizard.Active = true;

        Assert.Equal(QuickOutcome.Blocked, s.Quick.SetBlueLight(3400).Outcome);
        Assert.Empty(s.Log);
    }

    // ---- Panik ----

    [Fact]
    public void Panic_CallsOnlyPanicOff()
    {
        var s = new Setup();

        var result = s.Quick.Panic();

        Assert.Equal(QuickOutcome.Done, result.Outcome);
        Assert.Equal(["panic"], s.Log); // profil, parlaklık, Kelvin yazılmaz; kalibrasyon ve arka ışık kalır
    }

    [Fact]
    public void Panic_WorksWhileWizardIsActive()
    {
        var s = new Setup();
        s.Wizard.Active = true;

        Assert.Equal(QuickOutcome.Done, s.Quick.Panic().Outcome);
        Assert.Equal(["panic"], s.Log);
    }

    [Fact]
    public void Panic_Error_IsFailedWithMessage_NotSwallowed()
    {
        var s = new Setup();
        s.Profiles.PanicError = new InvalidOperationException("tablo yazılamadı");

        var result = s.Quick.Panic();

        Assert.Equal(QuickOutcome.Failed, result.Outcome);
        Assert.Equal("tablo yazılamadı", result.Error);
    }

    // ---- Sınırlar ----

    [Fact]
    public void QuickControls_DependsOnlyOnProfilesBrightnessBlueLightAndWizard()
    {
        // Hz (ekran modları), Güvenlik ve renk boru hattı (kalibrasyon) bu sınıfa hiç verilmez.
        var dependencies = typeof(QuickControls).GetConstructors().Single().GetParameters().Select(p => p.ParameterType);

        Assert.Equal(
            [typeof(IColorProfiles), typeof(IBrightnessControl), typeof(IBlueLightFilter), typeof(ISetupWizard)],
            dependencies);
    }
}
