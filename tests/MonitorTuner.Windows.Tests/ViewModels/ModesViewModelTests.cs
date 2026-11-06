using MonitorTuner.App.Localization;
using MonitorTuner.App.ViewModels;
using MonitorTuner.Services;

namespace MonitorTuner.Windows.Tests.ViewModels;

/// <summary>
/// Ana penceredeki görüntü modları (T319): renk profilleri (T308) artık sadece tepside değil, ana sekmede tek tıkla. Liste, ad
/// çevirisi, ne işe yaradığı, ne ayarladığı, tıklamanın sıradan servise gidişi ve kullanıcıya verilen sonuç.
/// </summary>
public class ModesViewModelTests
{
    private sealed class StubQuick : IQuickControls
    {
        public bool Enabled { get; set; } = true;
        public List<ColorProfile> List { get; } = [.. ColorProfiles.BuiltIns, new("Benim", false, 0.4, 4500, null)];
        public List<string> Calls { get; } = [];
        public QuickResult Next { get; set; } = new(QuickOutcome.Done);
        public Exception? Throw { get; set; }

        public IReadOnlyList<ColorProfile> Profiles => List;
        public ActiveMode? Active { get; set; }
        public double? ReadBrightness() => 0.6;
        public int BlueLightKelvin => 6500;

        public Action<string>? OnSelect { get; set; }

        public QuickResult SelectProfile(string name)
        {
            Calls.Add("profile:" + name);
            if (Throw is { } ex) throw ex;
            OnSelect?.Invoke(name);
            return Next;
        }

        public QuickResult SetBrightness(double slider) => throw new NotSupportedException();
        public QuickResult SetBlueLight(int kelvin) => throw new NotSupportedException();
        public QuickResult Panic() => throw new NotSupportedException();
    }

    private static (ModesViewModel Vm, StubQuick Quick) Make()
    {
        var quick = new StubQuick();
        return (new ModesViewModel(quick), quick);
    }

    private static ModeItem Mode(ModesViewModel vm, string name) => vm.Modes.Single(m => m.Name == name);

    [Fact]
    public void Modes_ListBuiltInsThenUserProfiles_WithTranslatedTitles()
    {
        var (vm, _) = Make();

        Assert.Equal(
        [
            "Film", "Sinema", "Sinema · Karanlık oda", "Sinema · Canlı", "Sinema · Gece",
            "Konfor · Gündüz", "Konfor · Akşam", "Konfor · Gece",
            "Gündüz", "Akşam", "Gece", "Kod", "Oyun", "DaVinci zaman çizelgesi", "Benim",
        ], vm.Modes.Select(m => m.Title));
        Assert.Equal("Benim", vm.Modes[^1].Name);
    }

    /// <summary>Gruplar (T321) arayüz sırasında; kullanıcı profili listede nerede olursa olsun "Kendi modların"da, en sonda.</summary>
    [Fact]
    public void Modes_AreOrderedByGroup_WithGroupTitles()
    {
        var quick = new StubQuick();
        quick.List.Insert(0, new ColorProfile("Önce", false, 0.5, null, null));

        var vm = new ModesViewModel(quick);

        Assert.Equal(["Sinema", "Uzun kullanım · göz konforu", "Günlük", "Özel", "Kendi modların"],
            vm.Modes.Select(m => m.GroupTitle).Distinct());
        Assert.Equal(["Önce", "Benim"], vm.Modes.Where(m => m.Group == ProfileGroup.User).Select(m => m.Name));
        Assert.Equal("Benim", vm.Modes[^1].Name);
    }

    [Fact]
    public void Groups_AreTranslated()
    {
        TestCulture.With("en-US", () =>
        {
            var (vm, _) = Make();
            Assert.Equal(["Cinema", "Long use · eye comfort", "Everyday", "Special", "Your modes"], vm.Modes.Select(m => m.GroupTitle).Distinct());
            Assert.Equal("Cinema · Dark room", vm.Modes.Single(m => m.Name == "Sinema · Karanlık oda").Title);
        });
    }

    [Fact]
    public void OnlyDay_IsRecommended()
    {
        var (vm, _) = Make();

        Assert.Equal(["Gündüz"], vm.Modes.Where(m => m.Recommended).Select(m => m.Name));
    }

    /// <summary>Her hazır modun ne zaman kullanılacağı yazar (araştırma: 08-GORUNTU-YONTEMLERI.md, 00-DONANIM.md); kullanıcınınkinde yok.</summary>
    [Fact]
    public void BuiltIns_SayWhatTheyAreFor_UserProfilesDoNot()
    {
        var (vm, _) = Make();

        Assert.All(vm.Modes.Take(ColorProfiles.BuiltIns.Count), m =>
        {
            Assert.True(m.HasPurpose, m.Name);
            Assert.DoesNotContain("[[", m.Purpose, StringComparison.Ordinal);
        });
        Assert.False(Mode(vm, "Benim").HasPurpose);
        Assert.Contains("6500K", Mode(vm, "Gündüz").Purpose);
    }

    [Fact]
    public void Values_ShowBacklightBlueLightAndVibrance()
    {
        var (vm, _) = Make();

        Assert.Equal("Arka ışık %50 · Mavi ışık kapalı (6500K) · Canlılık yok", Mode(vm, "Gündüz").Values);
        Assert.Equal("Arka ışık %38 · Mavi ışık kapalı (6500K) · Canlılık %60", Mode(vm, "Film").Values);
        // Yüzde, monitöre gerçekten yazılan değer: DimmingPlanner 0,30'u 12'ye çevirir (12,4999… kayan nokta).
        Assert.Equal("Arka ışık %12 · Mavi ışık 2700K · Canlılık yok", Mode(vm, "Gece").Values);
        Assert.Equal("Arka ışık %25 · Mavi ışık 4500K · Canlılık yok", Mode(vm, "Benim").Values);
    }

    /// <summary>Ton eğrisi (T320) değerlerde adıyla görünür; Standart ton yazılmaz.</summary>
    [Fact]
    public void Values_ShowTheToneCurve_WhenItIsNotStandard()
    {
        var (vm, _) = Make();

        Assert.Equal("Arka ışık %27 · Mavi ışık kapalı (6500K) · Sinema tonu (gamma 2.4) · Canlılık %56", Mode(vm, "Sinema").Values);
        Assert.Equal("Arka ışık %44 · Mavi ışık kapalı (6500K) · Canlı kontrast · Canlılık %68", Mode(vm, "Sinema · Canlı").Values);
        Assert.DoesNotContain("ton", Mode(vm, "Konfor · Akşam").Values, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Values_BelowTheBacklightFloor_SayExtraDimming_AndUntouchedSettingsAreLeftOut()
    {
        var quick = new StubQuick();
        quick.List.Clear();
        quick.List.Add(new ColorProfile("Karanlık", false, 0.1, null, 70));
        quick.List.Add(new ColorProfile("Sadece renk", false, null, 3400, null));

        var vm = new ModesViewModel(quick);

        Assert.Equal("Arka ışık 0 + ek karartma · Canlılık %70", vm.Modes[0].Values);
        Assert.Equal("Mavi ışık 3400K · Canlılık yok", vm.Modes[1].Values);
    }

    /// <summary>Arayüzde çeviri görünür, servise Türkçe sabit ad gider (tepsiyle aynı kural, T502).</summary>
    [Fact]
    public void Apply_SendsTheFixedName_AndReportsDone_InTheUiLanguage()
    {
        TestCulture.With("en-US", () =>
        {
            var (vm, quick) = Make();
            var evening = vm.Modes.Single(m => m.Title == "Evening");

            vm.Apply(evening);

            Assert.Equal(["profile:Akşam"], quick.Calls);
            Assert.Equal("Evening mode applied.", vm.Status);
        });
    }

    [Fact]
    public void Apply_Done_InTurkish()
    {
        var (vm, quick) = Make();

        vm.Apply(Mode(vm, "Film"));

        Assert.Equal(["profile:Film"], quick.Calls);
        Assert.Equal("Film modu uygulandı.", vm.Status);
    }

    [Fact]
    public void Apply_Partial_NamesWhatWasNotApplied_NeverSaysDone()
    {
        var (vm, quick) = Make();
        quick.Next = new QuickResult(QuickOutcome.Partial,
            new ProfileApplyResult("Film", ProfileStep.Applied, ProfileStep.Deferred, ProfileStep.Failed));

        vm.Apply(Mode(vm, "Film"));

        Assert.Equal("Film modu tam uygulanamadı. Uygulanmayan: mavi ışık, canlılık.", vm.Status);
    }

    [Fact]
    public void Apply_WhileWizardIsOpen_SaysWhy()
    {
        var (vm, quick) = Make();
        quick.Next = new QuickResult(QuickOutcome.Blocked);

        vm.Apply(Mode(vm, "Gündüz"));

        Assert.Contains("sihirbazı açıkken", vm.Status);
    }

    [Fact]
    public void Apply_UnexpectedError_IsShown_NotThrown()
    {
        var (vm, quick) = Make();
        quick.Throw = new InvalidOperationException("DDC kilitlendi");

        var task = vm.Apply(Mode(vm, "Gündüz"));

        Assert.True(task.IsCompletedSuccessfully);
        Assert.Contains("DDC kilitlendi", vm.Status);
        Assert.False(vm.IsBusy);
    }

    /// <summary>T316 ile aynı: servis arka plandaki sırada çalışır; bitene kadar liste kapalı görünür.</summary>
    [Fact]
    public void Apply_RunsOnTheLane_AndIsBusyUntilDone()
    {
        var quick = new StubQuick();
        var lane = new ManualLane();
        var vm = new ModesViewModel(quick, lane);

        var task = vm.Apply(vm.Modes[0]);

        Assert.True(vm.IsBusy);
        Assert.False(vm.IsReady);
        Assert.Empty(quick.Calls);

        lane.RunAll();

        Assert.True(task.IsCompleted);
        Assert.Equal(["profile:Film"], quick.Calls); // listenin ilki: Sinema grubunun ilk modu (T321)
        Assert.False(vm.IsBusy);
        Assert.Equal("Film modu uygulandı.", vm.Status);
    }

    [Fact]
    public void Reload_PicksUpChangedProfiles()
    {
        var (vm, quick) = Make();
        quick.List.Add(new ColorProfile("Yeni", false, 0.5, null, null));

        vm.Reload();

        Assert.Equal("Yeni", vm.Modes[^1].Name);
    }

    // ---- T322: gruplar, etkin mod ----

    /// <summary>Kartlar gruplarıyla: başlık, simge; ipucu sadece sinema (72 Hz) ve uzun kullanımda (oda ışığı, 20-20-20).</summary>
    [Fact]
    public void Groups_HoldTheCards_WithHintsOnlyForCinemaAndComfort()
    {
        var (vm, _) = Make();

        Assert.Equal(
            [ProfileGroup.Cinema, ProfileGroup.Comfort, ProfileGroup.Daily, ProfileGroup.Special, ProfileGroup.User],
            vm.Groups.Select(g => g.Group));
        Assert.Equal(vm.Modes.Select(m => m.Name), vm.Groups.SelectMany(g => g.Modes).Select(m => m.Name));
        Assert.Contains("72 Hz", vm.Groups[0].Hint);
        Assert.Contains("20-20-20", vm.Groups[1].Hint);
        Assert.Equal([true, true, false, false, false], vm.Groups.Select(g => g.HasHint));
        Assert.All(vm.Groups, g => Assert.Single(g.Icon));
    }

    [Fact]
    public void ActiveText_SaysNoModeYet_WhenNothingWasSelected()
    {
        var (vm, _) = Make();

        Assert.StartsWith("Ekranda mod yok", vm.ActiveText);
        Assert.DoesNotContain(vm.Modes, m => m.IsActive);
    }

    /// <summary>Ekrandaki mod kartında işaretli ve üstte adıyla yazar; elle değiştirildiyse söylenir.</summary>
    [Fact]
    public void ActiveMode_IsMarked_AndNamed_InTheUiLanguage()
    {
        var (vm, quick) = Make();
        quick.Active = new ActiveMode("Sinema · Karanlık oda", Adjusted: false);
        vm.Reload();

        Assert.Equal(["Sinema · Karanlık oda"], vm.Modes.Where(m => m.IsActive).Select(m => m.Name));
        Assert.Equal("Ekranda: Sinema · Karanlık oda", vm.ActiveText);

        quick.Active = quick.Active with { Adjusted = true };
        vm.Reload();
        Assert.Contains("elle değişti", vm.ActiveText);

        TestCulture.With("en-US", () =>
        {
            vm.Reload();
            Assert.StartsWith("On screen: Cinema · Dark room", vm.ActiveText);
        });
    }

    /// <summary>Seçimden sonra liste yeniden kurulur: yeni etkin mod işaretli olur.</summary>
    [Fact]
    public void Apply_MarksTheNewActiveMode()
    {
        var (vm, quick) = Make();
        quick.OnSelect = name => quick.Active = new ActiveMode(name, false);

        vm.Apply(Mode(vm, "Sinema"));

        Assert.True(Mode(vm, "Sinema").IsActive);
        Assert.Equal("Ekranda: Sinema", vm.ActiveText);
    }

    /// <summary>Pencere öne gelince etkin mod değişmediyse liste yeniden kurulmaz (ilk tıklama kaybolmasın); değiştiyse kurulur.</summary>
    [Fact]
    public void RefreshActive_RebuildsOnlyWhenTheActiveModeChanged()
    {
        var (vm, quick) = Make();
        var before = vm.Modes[0];

        vm.RefreshActive();
        Assert.Same(before, vm.Modes[0]);

        quick.Active = new ActiveMode("Film", false); // tepsiden seçildi
        vm.RefreshActive();
        Assert.NotSame(before, vm.Modes[0]);
        Assert.True(Mode(vm, "Film").IsActive);
    }

    [Fact]
    public void AllTexts_ExistInTurkishResources()
    {
        string[] keys =
        [
            "Modes.Title", "Modes.Intro", "Modes.Recommended", "Modes.BestTitle", "Modes.BestStep1", "Modes.BestStep2",
            "Modes.BestStep3", "Modes.Done", "Modes.Partial", "Modes.Blocked", "Modes.NotFound", "Modes.Failed", "Modes.Unexpected",
            "Modes.Value_Backlight", "Modes.Value_Dimming", "Modes.Value_BlueLightOff", "Modes.Value_Kelvin", "Modes.Value_Vibrance",
            "Modes.Value_VibranceOff", "Common.HomeText", "Modes.Value_ToneCinema", "Modes.Value_ToneContrast",
            "Modes.GroupHint_Cinema", "Modes.GroupHint_Comfort", "Modes.Active", "Modes.Active_Adjusted", "Modes.Active_None",
            "Modes.ActiveBadge",
            .. Enum.GetValues<ProfileGroup>().Select(g => "Modes.Group_" + g),
        ];

        var missing = keys.Where(k => Loc.Get(k).StartsWith("[[", StringComparison.Ordinal)).ToList();

        Assert.Empty(missing);
    }
}
