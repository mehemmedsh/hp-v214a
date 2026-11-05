using MonitorTuner.App.ViewModels;
using MonitorTuner.Color;
using MonitorTuner.Services;

namespace MonitorTuner.Windows.Tests.ViewModels;

/// <summary>Tepsi menüsü (T310): içerik, sihirbaz açıkken kapalı ögeler, tıklamaların servise gidişi, kullanıcıya verilen iletiler.</summary>
public class TrayViewModelTests
{
    private sealed class StubQuick : IQuickControls
    {
        public bool Enabled { get; set; } = true;
        public List<ColorProfile> List { get; } = [.. ColorProfiles.BuiltIns, new("Benim", false, 0.4, 4500, null)];
        public List<string> Calls { get; } = [];
        public QuickResult Next { get; set; } = new(QuickOutcome.Done);

        public IReadOnlyList<ColorProfile> Profiles => List;

        public QuickResult SelectProfile(string name)
        {
            Calls.Add("profile:" + name);
            return Next;
        }

        public QuickResult SetBrightness(double slider)
        {
            Calls.Add("brightness:" + slider.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            return Next;
        }

        public QuickResult SetBlueLight(int kelvin)
        {
            Calls.Add("kelvin:" + kelvin);
            return Next;
        }

        public QuickResult Panic()
        {
            Calls.Add("panic");
            return Next;
        }
    }

    private static (TrayViewModel Vm, StubQuick Quick, List<string> Notes) Make()
    {
        var quick = new StubQuick();
        var vm = new TrayViewModel(quick, "Ctrl+Alt+Shift+P");
        var notes = new List<string>();
        vm.Notified += notes.Add;
        return (vm, quick, notes);
    }

    private static TrayItem Find(IReadOnlyList<TrayItem> menu, string text) => menu.Single(i => i.Text == text);

    [Fact]
    public void Menu_HasProfilesBrightnessDimmingBlueLightPanicOpenAndExit()
    {
        var (vm, _, _) = Make();

        var texts = vm.BuildMenu().Where(i => !i.IsSeparator).Select(i => i.Text).ToList();

        Assert.Equal(
            ["Renk profilleri", "Parlaklık", "Ek karartma", "Mavi ışık", "Panik: efektleri kapat (Ctrl+Alt+Shift+P)", "Ana pencereyi aç", "Çık"],
            texts);
    }

    [Fact]
    public void ProfileSubmenu_ListsBuiltInAndUserProfiles()
    {
        var (vm, _, _) = Make();

        var profiles = Find(vm.BuildMenu(), "Renk profilleri").Children!.Select(i => i.Text);

        Assert.Equal(["Gündüz", "Akşam", "Gece", "Kod", "Film", "Oyun", "DaVinci zaman çizelgesi", "Benim"], profiles);
    }

    /// <summary>T502: hazır profillerin adları arayüz dilinde görünür; servise Türkçe sabit ad gider. Kullanıcı profili olduğu gibi.</summary>
    [Fact]
    public void BuiltInProfileNames_AreTranslated_ButTheServiceGetsTheFixedName()
    {
        TestCulture.With("en-US", () =>
        {
            var (vm, quick, notes) = Make();
            var profiles = Find(vm.BuildMenu(), "Color profiles").Children!;

            Assert.Equal(["Day", "Evening", "Night", "Code", "Movie", "Game", "DaVinci timeline", "Benim"], profiles.Select(i => i.Text));

            profiles.Single(i => i.Text == "Evening").Click!();

            Assert.Equal(["profile:Akşam"], quick.Calls);
            Assert.Equal(["Evening profile applied."], notes);
        });
    }

    [Fact]
    public void ClickingProfile_SelectsItOnce_AndNotifiesDone()
    {
        var (vm, quick, notes) = Make();

        Find(vm.BuildMenu(), "Renk profilleri").Children!.Single(i => i.Text == "Film").Click!();

        Assert.Equal(["profile:Film"], quick.Calls);
        Assert.Equal(["Film profili uygulandı."], notes);
    }

    [Fact]
    public void ClickingBrightnessAndDimming_CommitsTheSliderValue()
    {
        var (vm, quick, notes) = Make();
        var menu = vm.BuildMenu();

        Find(menu, "Parlaklık").Children!.Single(i => i.Text == "Parlaklık %60").Click!();
        Find(menu, "Ek karartma").Children!.Single(i => i.Text == "En karanlık (beyaz %15)").Click!();

        Assert.Equal(["brightness:0.6", "brightness:0"], quick.Calls);
        Assert.Equal(["Parlaklık %60 yapıldı.", "Ek karartma: En karanlık (beyaz %15)."], notes);
    }

    [Fact]
    public void ClickingBlueLight_SetsKelvin_AndOffUses6500()
    {
        var (vm, quick, notes) = Make();
        var blue = Find(vm.BuildMenu(), "Mavi ışık").Children!;

        blue.Single(i => i.Text == "3400 K").Click!();
        blue.Single(i => i.Text == "Kapalı").Click!();

        Assert.Equal(["kelvin:3400", "kelvin:6500"], quick.Calls);
        Assert.Equal(["Mavi ışık 3400 K yapıldı.", "Mavi ışık kapatıldı."], notes);
    }

    // ---- Sihirbaz açıkken ----

    [Fact]
    public void WhileWizardIsActive_ProfileBrightnessDimmingAndBlueLightItemsAreDisabled_PanicStaysEnabled()
    {
        var (vm, quick, _) = Make();
        quick.Enabled = false;

        var menu = vm.BuildMenu();

        Assert.Contains(menu, i => i.Text.StartsWith("Ayar sihirbazı açık", StringComparison.Ordinal) && !i.Enabled);
        foreach (var submenu in new[] { "Renk profilleri", "Parlaklık", "Ek karartma", "Mavi ışık" })
        {
            var item = Find(menu, submenu);
            Assert.All(item.Children!, child => Assert.False(child.Enabled, $"{submenu} → {child.Text}"));
        }

        Assert.True(Find(menu, "Panik: efektleri kapat (Ctrl+Alt+Shift+P)").Enabled);
        Assert.True(Find(menu, "Ana pencereyi aç").Enabled);
        Assert.True(Find(menu, "Çık").Enabled);
    }

    [Fact]
    public void WhenEnabled_NoWizardNoticeIsShown_AndAllItemsAreEnabled()
    {
        var (vm, _, _) = Make();

        var menu = vm.BuildMenu();

        Assert.DoesNotContain(menu, i => i.Text.StartsWith("Ayar sihirbazı açık", StringComparison.Ordinal));
        Assert.All(Find(menu, "Renk profilleri").Children!, c => Assert.True(c.Enabled));
    }

    [Fact]
    public void BlockedResult_TellsTheUserWhy()
    {
        var (vm, quick, notes) = Make();
        var menu = vm.BuildMenu();
        quick.Next = new QuickResult(QuickOutcome.Blocked); // menü açıkken sihirbaz başladı

        Find(menu, "Renk profilleri").Children!.Single(i => i.Text == "Film").Click!();

        Assert.Equal(["Ayar sihirbazı açıkken bu ayar kapalı. Sihirbazı bitirin ya da iptal edin."], notes);
    }

    // ---- İletiler (yanlış bilgi yok) ----

    [Fact]
    public void PartialProfile_NamesWhatWasNotApplied_NeverSaysDone()
    {
        var (vm, quick, notes) = Make();
        quick.Next = new QuickResult(QuickOutcome.Partial,
            new ProfileApplyResult("Film", ProfileStep.Applied, ProfileStep.Deferred, ProfileStep.Failed));

        vm.SelectProfile("Film");

        Assert.Equal(["Film profili tam uygulanamadı. Uygulanmayan: mavi ışık, canlılık."], notes);
    }

    [Fact]
    public void FailedAndNotFound_AreReported()
    {
        var (vm, quick, notes) = Make();

        quick.Next = new QuickResult(QuickOutcome.Failed);
        vm.SetBlueLight(3400);
        quick.Next = new QuickResult(QuickOutcome.NotFound);
        vm.SelectProfile("Yok");

        Assert.Contains("uygulanamadı", notes[0]);
        Assert.Equal("Profil bulunamadı.", notes[1]);
    }

    [Fact]
    public void UnexpectedError_IsReported_NotThrown()
    {
        var quick = new ThrowingQuick();
        var vm = new TrayViewModel(quick, "Ctrl+Alt+Shift+P");
        var notes = new List<string>();
        vm.Notified += notes.Add;

        vm.SelectProfile("Film");

        Assert.Equal(["Beklenmeyen hata: bozuk"], notes);
    }

    private sealed class ThrowingQuick : IQuickControls
    {
        public bool Enabled => true;
        public IReadOnlyList<ColorProfile> Profiles => [];
        public QuickResult SelectProfile(string name) => throw new InvalidOperationException("bozuk");
        public QuickResult SetBrightness(double slider) => throw new NotSupportedException();
        public QuickResult SetBlueLight(int kelvin) => throw new NotSupportedException();
        public QuickResult Panic() => throw new NotSupportedException();
    }

    // ---- Panik ----

    [Fact]
    public void Panic_FromMenuAndFromHotKey_CallsPanicAndNotifies_EvenWhenWizardIsActive()
    {
        var (vm, quick, notes) = Make();
        quick.Enabled = false;

        Find(vm.BuildMenu(), "Panik: efektleri kapat (Ctrl+Alt+Shift+P)").Click!();
        vm.Panic();

        Assert.Equal(["panic", "panic"], quick.Calls);
        Assert.All(notes, n => Assert.Equal("Panik: mavi ışık, ek karartma ve canlılık kapatıldı. Kalibrasyon kaldı.", n));
        Assert.Equal(2, notes.Count);
    }

    [Fact]
    public void PanicFailure_IsReportedWithReason()
    {
        var (vm, quick, notes) = Make();
        quick.Next = new QuickResult(QuickOutcome.Failed, Error: "tablo yazılamadı");

        vm.Panic();

        Assert.StartsWith("Panik tamamlanamadı (tablo yazılamadı).", notes.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void OpenAndExit_CallTheirCallbacks()
    {
        var (vm, _, _) = Make();
        var calls = new List<string>();
        vm.OpenMainWindow = () => calls.Add("open");
        vm.Exit = () => calls.Add("exit");
        var menu = vm.BuildMenu();

        Find(menu, "Ana pencereyi aç").Click!();
        Find(menu, "Çık").Click!();

        Assert.Equal(["open", "exit"], calls);
    }

    [Theory]
    [InlineData("Tray.Tooltip")]
    [InlineData("Tray.HotKeyFailed")]
    [InlineData("Common.AppTitle")]
    public void TrayHostTexts_ExistInTurkish(string key) =>
        Assert.DoesNotContain("[[", MonitorTuner.App.Localization.Loc.Get(key), StringComparison.Ordinal);
}

public class TrayViewModelAnnounceTests
{
    private sealed class NoQuick : IQuickControls
    {
        public bool Enabled => true;
        public IReadOnlyList<ColorProfile> Profiles => [];
        public QuickResult SelectProfile(string name) => throw new NotSupportedException();
        public QuickResult SetBrightness(double slider) => throw new NotSupportedException();
        public QuickResult SetBlueLight(int kelvin) => throw new NotSupportedException();
        public QuickResult Panic() => throw new NotSupportedException();
    }

    /// <summary>T314: zamanlayıcının uyarıları (başka program renk tablosunu değiştirdi) tepsi balonuyla gider; bir ayar değişmez.</summary>
    [Fact]
    public void Announce_RaisesNotified_WithoutTouchingAnyService()
    {
        var vm = new TrayViewModel(new NoQuick(), "Ctrl+Alt+Shift+P");
        var notes = new List<string>();
        vm.Notified += notes.Add;

        vm.Announce("merhaba");

        Assert.Equal(["merhaba"], notes);
    }
}
