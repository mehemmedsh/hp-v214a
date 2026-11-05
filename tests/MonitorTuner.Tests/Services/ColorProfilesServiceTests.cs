using System.Globalization;
using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Services;

namespace MonitorTuner.Tests.Services;

/// <summary>
/// Renk profilleri (T308): sadece <see cref="IColorProfiles.Apply"/> çağrılınca, her ayarı bir kez, ilgili servis
/// üzerinden uygular. Hz'e ve kalibrasyona ulaşan hiçbir bağımlılığı yoktur. Sahte servislerle; gerçek sahte donanımla
/// uçtan uca sınama <c>Contracts/ColorProfilesTests</c>'te.
/// </summary>
public class ColorProfilesServiceTests
{
    private static readonly MonitorKey Key = new("HP P21b G4", "SN1", "HDMI", "RTX 3080");

    /// <summary>Arayüzdeki sırayla (T321): Sinema, Uzun kullanım (Konfor), Günlük, Özel.</summary>
    private static readonly string[] BuiltInNames =
    [
        "Film", "Sinema", "Sinema · Karanlık oda", "Sinema · Canlı", "Sinema · Gece",
        "Konfor · Gündüz", "Konfor · Akşam", "Konfor · Gece",
        "Gündüz", "Akşam", "Gece", "Kod",
        "Oyun", "DaVinci zaman çizelgesi",
    ];

    // ---- Sahteler (hepsi aynı olay günlüğüne yazar) ----

    private sealed class FakeBrightness(List<string> log) : IBrightnessControl
    {
        public bool Applied { get; set; } = true;
        public bool DimmingActive => false;
        public DimmingPlan Preview(double slider) => throw new NotSupportedException();

        public BrightnessState Commit(double slider)
        {
            log.Add(Brightness(slider));
            return new BrightnessState(new DimmingPlan(0, 1), Applied, null);
        }

        public double? ReadSlider() => throw new NotSupportedException();

        public bool DisableDimming()
        {
            log.Add("dimming:off");
            return true;
        }

        public bool Suspend() => throw new NotSupportedException();
        public bool Resume() => throw new NotSupportedException();
        public bool ResumeAt(double slider) => throw new NotSupportedException();
        public bool Shutdown() => throw new NotSupportedException();
    }

    private sealed class FakeBlueLight(List<string> log) : IBlueLightFilter
    {
        public bool Applied { get; set; } = true;
        public bool Suspended { get; set; }
        public int Kelvin { get; private set; } = 6500;
        public bool IsSuspended => Suspended;
        public EveningSettings Evening { get; set; } = EveningSettings.Default;

        public BlueLightState SetKelvin(int kelvin)
        {
            log.Add($"kelvin:{kelvin}");
            Kelvin = kelvin;
            return new BlueLightState(kelvin, Applied, RgbGains.Neutral, RgbGains.Neutral, []);
        }

        public BlueLightState Disable()
        {
            log.Add("bluelight:off");
            return SetKelvinQuiet(6500);
        }

        private BlueLightState SetKelvinQuiet(int kelvin)
        {
            Kelvin = kelvin;
            return new BlueLightState(kelvin, Applied, RgbGains.Neutral, RgbGains.Neutral, []);
        }

        public BlueLightState Suspend() => throw new NotSupportedException();
        public BlueLightState Resume() => throw new NotSupportedException();
        public BlueLightState Shutdown() => throw new NotSupportedException();
        public LowBlueLightResult SelectMonitorLowBlueLight() => throw new NotSupportedException();
        public IReadOnlyList<ColorConflict> CheckConflicts() => throw new NotSupportedException();
        public bool Tick() => throw new NotSupportedException();
    }

    private sealed class FakeNvidia(List<string> log) : INvidiaControl
    {
        public bool SetResult { get; set; } = true;
        public bool ResetResult { get; set; } = true;
        public int? Vibrance { get; private set; }

        public bool SetVibrance(int percent)
        {
            log.Add($"vibrance:{percent}");
            Vibrance = percent;
            return SetResult;
        }

        public bool ResetVibrance()
        {
            log.Add("vibrance:reset");
            Vibrance = null;
            return ResetResult;
        }

        public void Suspend() => throw new NotSupportedException();
        public void Resume() => throw new NotSupportedException();
        public void Shutdown() => throw new NotSupportedException();
    }

    private sealed class FakeTone(List<string> log) : IToneControl
    {
        public bool Result { get; set; } = true;
        public bool Suspended { get; set; }
        public ToneStyle Style { get; private set; }
        public bool IsSuspended => Suspended;

        public bool Set(ToneStyle style)
        {
            log.Add($"tone:{style}");
            Style = style;
            return Result && !Suspended;
        }

        public bool Disable()
        {
            log.Add("tone:off");
            Style = ToneStyle.Standard;
            return true;
        }

        public bool Suspend() => throw new NotSupportedException();
        public bool Resume() => throw new NotSupportedException();
    }

    private sealed class FakeStore(List<string> log) : ISettingsStore
    {
        public Dictionary<string, object> Sections { get; } = [];
        public string BackupFolder => "";

        public T? Read<T>(MonitorKey monitor, string section) =>
            Sections.TryGetValue(section, out var v) && v is T t ? t : default;

        public void Write<T>(MonitorKey monitor, string section, T value)
        {
            log.Add($"store:{section}");
            Sections[section] = value!;
        }
    }

    private sealed class Rig
    {
        public List<string> Log { get; } = [];
        public FakeBrightness Brightness { get; }
        public FakeBlueLight BlueLight { get; }
        public FakeNvidia Nvidia { get; }
        public FakeTone Tone { get; }
        public FakeStore Store { get; }
        public ColorProfiles Profiles { get; private set; }

        public Rig()
        {
            Brightness = new FakeBrightness(Log);
            BlueLight = new FakeBlueLight(Log);
            Nvidia = new FakeNvidia(Log);
            Tone = new FakeTone(Log);
            Store = new FakeStore(Log);
            Profiles = Create();
        }

        /// <summary>Aynı depoyla yeni örnek: uygulama yeniden açıldı.</summary>
        public ColorProfiles Reopen() => Profiles = Create();

        private ColorProfiles Create() => new(Brightness, BlueLight, Nvidia, Tone, Store, Key);

        public ColorProfile Built(string name) => Profiles.Profiles.Single(p => p.Name == name);
    }

    /// <summary>Sahte günlük girdisi; kültürden bağımsız (testler tr-TR makinede de geçer).</summary>
    private static string Brightness(double slider) => "brightness:" + slider.ToString(CultureInfo.InvariantCulture);

    private static ColorProfile User(string name, double? brightness = 0.5, int? kelvin = 4000, int? vibrance = null) =>
        new(name, false, brightness, kelvin, vibrance);

    // ---- Hazır profiller ----

    [Fact]
    public void Profiles_ListsTheBuiltIns_InOrder_AllMarkedBuiltIn()
    {
        var rig = new Rig();

        Assert.Equal(BuiltInNames, rig.Profiles.Profiles.Select(p => p.Name));
        Assert.All(rig.Profiles.Profiles, p => Assert.True(p.BuiltIn));
    }

    /// <summary>Her hazır modun grubu var, gruplar arayüz sırasında art arda (T321); hiçbir hazır mod "Kendi" grubunda değil.</summary>
    [Fact]
    public void BuiltIns_AreGrouped_InTheDisplayOrder()
    {
        var groups = ColorProfiles.BuiltIns.Select(p => p.Group).ToList();

        Assert.Equal(
            [ProfileGroup.Cinema, ProfileGroup.Comfort, ProfileGroup.Daily, ProfileGroup.Special],
            groups.Distinct());
        Assert.Equal(groups.OrderBy(g => ColorProfiles.GroupOrder.ToList().IndexOf(g)), groups);
        Assert.Equal([ProfileGroup.Cinema, ProfileGroup.Comfort, ProfileGroup.Daily, ProfileGroup.Special, ProfileGroup.User],
            ColorProfiles.GroupOrder);
        Assert.DoesNotContain(ProfileGroup.User, groups);
    }

    /// <summary>
    /// Sinema (T321, 08-GORUNTU-YONTEMLERI.md → 6): beyaz 6500K (filmler D65 ile hazırlanır; yalnız Gece ılık), loş ve karanlık
    /// oda gamma 2,4 (BT.1886), Canlı kontrast eğrisi; Film aynen (kullanıcı beğendi). Parlaklık odaya göre: Canlı > Film >
    /// Sinema > Karanlık oda > Gece, hiçbiri ek karartmaya inmez (GPU karartması kontrastı düşürür).
    /// </summary>
    [Fact]
    public void Cinema_UsesD65_TheCinemaGamma_AndDimsWithTheRoom()
    {
        var rig = new Rig();
        ColorProfile film = rig.Built("Film"), cinema = rig.Built("Sinema"), dark = rig.Built("Sinema · Karanlık oda"),
            vivid = rig.Built("Sinema · Canlı"), night = rig.Built("Sinema · Gece");

        Assert.Equal(new ColorProfile("Film", true, 0.50, KelvinCalculator.OffKelvin, 60, ToneStyle.Standard, ProfileGroup.Cinema), film);
        Assert.All(new[] { cinema, dark, night }, p => Assert.Equal(ToneStyle.Cinema, p.Tone));
        Assert.Equal(ToneStyle.Contrast, vivid.Tone);
        Assert.All(new[] { film, cinema, dark, vivid }, p => Assert.Equal(KelvinCalculator.OffKelvin, p.Kelvin));
        Assert.InRange(night.Kelvin!.Value, 4000, 5000); // yatmadan önce: daha az mavi ama uzun izlemede yormayacak kadar ılımlı

        Assert.True(vivid.Brightness > film.Brightness && film.Brightness > cinema.Brightness);
        Assert.True(cinema.Brightness > dark.Brightness && dark.Brightness > night.Brightness);
        Assert.All(new[] { film, cinema, dark, vivid, night }, p => Assert.True(p.Brightness >= DimmingPlanner.TransitionPoint, p.Name));
    }

    /// <summary>Karanlıkta renkler soluk algılanır (Hunt etkisi): karanlık oda loş odadan biraz daha canlı; canlılık hep ölçülü (≤ 70).</summary>
    [Fact]
    public void Cinema_DarkRoom_GetsALittleMoreVibrance()
    {
        var rig = new Rig();

        Assert.True(rig.Built("Sinema · Karanlık oda").Vibrance > rig.Built("Sinema").Vibrance);
        Assert.All(rig.Profiles.Profiles.Where(p => p.Group == ProfileGroup.Cinema), p => Assert.InRange(p.Vibrance!.Value, 55, 70));
    }

    /// <summary>
    /// Uzun kullanım (T321): efekt yok (ton Standart, canlılık yok), ılımlı sıcaklık (4000–5500K: 2800K uzun okumada en çok
    /// yoruyor), parlaklık odayla birlikte iner ama ek karartmaya inmez.
    /// </summary>
    [Fact]
    public void Comfort_IsEffectFree_ModeratelyWarm_AndDimsWithTheRoom()
    {
        var rig = new Rig();
        ColorProfile day = rig.Built("Konfor · Gündüz"), evening = rig.Built("Konfor · Akşam"), night = rig.Built("Konfor · Gece");

        Assert.All(new[] { day, evening, night }, p =>
        {
            Assert.Equal(ProfileGroup.Comfort, p.Group);
            Assert.Equal(ToneStyle.Standard, p.Tone);
            Assert.Null(p.Vibrance);
            Assert.InRange(p.Kelvin!.Value, 4000, 5500);
            Assert.True(p.Brightness >= DimmingPlanner.TransitionPoint, p.Name);
        });
        Assert.True(day.Brightness > evening.Brightness && evening.Brightness > night.Brightness);
        Assert.True(day.Kelvin > evening.Kelvin && evening.Kelvin >= night.Kelvin);
    }

    /// <summary>Kullanıcının kaydettiği her profil "Kendi" grubundadır; dosyada başka grup yazsa da.</summary>
    [Fact]
    public void UserProfiles_AreAlwaysInTheUserGroup()
    {
        var rig = new Rig();
        rig.Profiles.Save(User("Benim") with { Group = ProfileGroup.Cinema });
        rig.Store.Sections[ColorProfiles.Section] = new ColorProfilesRecord(
            [.. rig.Store.Read<ColorProfilesRecord>(Key, ColorProfiles.Section)!.Profiles, User("Elle") with { Group = ProfileGroup.Daily }]);

        var reopened = rig.Reopen().Profiles.Where(p => !p.BuiltIn).ToList();

        Assert.Equal(["Benim", "Elle"], reopened.Select(p => p.Name));
        Assert.All(reopened, p => Assert.Equal(ProfileGroup.User, p.Group));
    }

    [Fact]
    public void BuiltIns_AreAllValid()
    {
        var rig = new Rig();

        foreach (var profile in rig.Profiles.Profiles) ColorProfiles.Validate(profile); // atmamalı
    }

    /// <summary>Canlılık sadece sinema modlarında ve Oyun'da açık; diğer hazır profillerde null (kapalı).</summary>
    [Fact]
    public void BuiltIns_VibranceOnlyInCinemaAndGame()
    {
        var rig = new Rig();

        foreach (var profile in rig.Profiles.Profiles)
            Assert.Equal(profile.Group == ProfileGroup.Cinema || profile.Name == "Oyun", profile.Vibrance is not null);
    }

    /// <summary>DaVinci zaman çizelgesi: nötr ve efektsiz (mavi ışık kapalı, canlılık yok).</summary>
    [Fact]
    public void BuiltIn_DaVinci_IsNeutral()
    {
        var rig = new Rig();

        var davinci = rig.Built("DaVinci zaman çizelgesi");

        Assert.Equal(KelvinCalculator.OffKelvin, davinci.Kelvin);
        Assert.Null(davinci.Vibrance);
        Assert.True(davinci.Brightness >= DimmingPlanner.TransitionPoint); // ek karartma da kapalı
    }

    /// <summary>Gece = mavi ışık + düşük parlaklık: en sıcak ve Akşam'dan karanlık; Akşam Gündüz'den sıcak ve karanlık.</summary>
    [Fact]
    public void BuiltIns_DayEveningNight_GetWarmerAndDarker()
    {
        var rig = new Rig();
        ColorProfile day = rig.Built("Gündüz"), evening = rig.Built("Akşam"), night = rig.Built("Gece");

        Assert.True(night.Kelvin < evening.Kelvin && evening.Kelvin < day.Kelvin);
        Assert.True(night.Brightness < evening.Brightness && evening.Brightness < day.Brightness);
        Assert.True(night.Brightness >= DimmingPlanner.TransitionPoint); // hazır profil derin karartmaya inmez
    }

    /// <summary>Profil kaydında Hz alanı yok; tür düzeyinde de Hz'e ulaşacak bir bağımlılık yok.</summary>
    [Fact]
    public void Profile_HasNoRefreshRateField_AndService_HasNoDisplayDependencies()
    {
        var fields = typeof(ColorProfile).GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain(fields, f => f.Contains("Hz", StringComparison.OrdinalIgnoreCase)
                                           || f.Contains("Refresh", StringComparison.OrdinalIgnoreCase)
                                           || f.Contains("Mode", StringComparison.OrdinalIgnoreCase));

        // Ton servisi (T320) yalnız ton katmanına yazar; kalibrasyona ve ekran modlarına ulaşmaz.
        var dependencies = typeof(ColorProfiles).GetConstructors().Single().GetParameters().Select(p => p.ParameterType);
        Assert.Equal(
            new[]
            {
                typeof(IBrightnessControl), typeof(IBlueLightFilter), typeof(INvidiaControl), typeof(IToneControl),
                typeof(ISettingsStore), typeof(MonitorKey),
            },
            dependencies);
    }

    // ---- Sadece kullanıcı seçince ----

    /// <summary>Kurulum, listeleme, kaydetme, silme ve yeniden açılış hiçbir ayarı uygulamaz.</summary>
    [Fact]
    public void NothingIsApplied_UntilTheUserApplies()
    {
        var rig = new Rig();

        _ = rig.Profiles.Profiles;
        rig.Profiles.Save(User("Benim"));
        rig.Reopen();
        _ = rig.Profiles.Profiles;
        rig.Profiles.Delete("Benim");

        Assert.DoesNotContain(rig.Log, e => !e.StartsWith("store:", StringComparison.Ordinal)); // sadece depo yazmaları
    }

    // ---- Uygulama ----

    [Fact]
    public void Apply_Day_CommitsBrightness_SetsKelvin_StandardTone_ResetsVibrance_OnceEach_InThatOrder()
    {
        var rig = new Rig();
        var day = rig.Built("Gündüz");

        var result = rig.Profiles.Apply(day);

        Assert.Equal([Brightness(day.Brightness!.Value), $"kelvin:{day.Kelvin}", "tone:Standard", "vibrance:reset"], rig.Log);
        Assert.True(result.Success);
        Assert.True(result.Complete);
        Assert.Equal(
            new ProfileApplyResult("Gündüz", ProfileStep.Applied, ProfileStep.Applied, ProfileStep.Applied, ProfileStep.Applied), result);
    }

    /// <summary>Profilin tonu (T320) ton servisine gider; askıdaysa ertelenir, reddedilirse başarısız sayılır, diğerleri yine uygulanır.</summary>
    [Fact]
    public void Apply_Tone_IsSet_DeferredWhileSuspended_FailedWhenRejected()
    {
        var rig = new Rig();
        var cinema = User("Sinema deneme") with { Tone = ToneStyle.Cinema };

        Assert.Equal(ProfileStep.Applied, rig.Profiles.Apply(cinema).Tone);
        Assert.Equal(ToneStyle.Cinema, rig.Tone.Style);

        rig.Tone.Suspended = true;
        var deferred = rig.Profiles.Apply(cinema);
        Assert.Equal(ProfileStep.Deferred, deferred.Tone);
        Assert.True(deferred.Success);
        Assert.False(deferred.Complete);

        rig.Tone.Suspended = false;
        rig.Tone.Result = false;
        var failed = rig.Profiles.Apply(cinema);
        Assert.Equal(ProfileStep.Failed, failed.Tone);
        Assert.False(failed.Success);
        Assert.Contains("vibrance:reset", rig.Log[^1]); // ton başarısız olsa da canlılık uygulandı
    }

    /// <summary>Elle bozulmuş kayıttaki bilinmeyen ton geçersizdir: hiçbir şey yazılmaz.</summary>
    [Fact]
    public void Validate_RejectsUnknownTone()
    {
        var rig = new Rig();
        Assert.Throws<ArgumentException>(() => rig.Profiles.Apply(User("Bozuk") with { Tone = (ToneStyle)7 }));
        Assert.Empty(rig.Log);
    }

    [Theory]
    [InlineData("Film")]
    [InlineData("Oyun")]
    public void Apply_FilmAndGame_SetVibrance_InsteadOfReset(string name)
    {
        var rig = new Rig();
        var profile = rig.Built(name);

        rig.Profiles.Apply(profile);

        Assert.Contains($"vibrance:{profile.Vibrance}", rig.Log);
        Assert.DoesNotContain("vibrance:reset", rig.Log);
    }

    /// <summary>Canlılığı olmayan her profil, uygulamanın açtığı canlılığı kapatır (kullanıcının değerine döner).</summary>
    [Fact]
    public void Apply_ProfileWithoutVibrance_ResetsIt()
    {
        var rig = new Rig();
        rig.Profiles.Apply(rig.Built("Film"));

        rig.Profiles.Apply(rig.Built("DaVinci zaman çizelgesi"));

        Assert.Equal("vibrance:reset", rig.Log[^1]);
        Assert.Null(rig.Nvidia.Vibrance);
        Assert.Equal(KelvinCalculator.OffKelvin, rig.BlueLight.Kelvin);
    }

    /// <summary>Her hazır profilde her ayar tam bir kez uygulanır; arkada devam eden iş (zamanlayıcı) yoktur.</summary>
    [Fact]
    public void Apply_EveryBuiltIn_WritesEachSettingExactlyOnce()
    {
        foreach (var name in BuiltInNames)
        {
            var rig = new Rig();
            rig.Profiles.Apply(rig.Built(name));

            Assert.Equal(1, rig.Log.Count(e => e.StartsWith("brightness:", StringComparison.Ordinal)));
            Assert.Equal(1, rig.Log.Count(e => e.StartsWith("kelvin:", StringComparison.Ordinal)));
            Assert.Equal(1, rig.Log.Count(e => e.StartsWith("vibrance:", StringComparison.Ordinal)));
            Assert.Equal(1, rig.Log.Count(e => e.StartsWith("tone:", StringComparison.Ordinal)));
            Assert.Equal(4, rig.Log.Count);
        }
    }

    /// <summary>
    /// null parlaklık ve null Kelvin: profil o ayara dokunmaz. Ton bir efekttir (canlılık gibi): profilde yoksa Standart, yani
    /// önceki profilin sinema eğrisi kapanır.
    /// </summary>
    [Fact]
    public void Apply_NullBrightnessAndKelvin_TouchNeitherBacklightNorBlueLight()
    {
        var rig = new Rig();

        var result = rig.Profiles.Apply(User("Sadece canlılık", brightness: null, kelvin: null, vibrance: 70));

        Assert.Equal(["tone:Standard", "vibrance:70"], rig.Log);
        Assert.Equal(
            new ProfileApplyResult("Sadece canlılık", ProfileStep.Skipped, ProfileStep.Skipped, ProfileStep.Applied, ProfileStep.Applied),
            result);
    }

    [Fact]
    public void Apply_ReportsFailures_ButStillAppliesTheOtherSettings()
    {
        var rig = new Rig();
        rig.Brightness.Applied = false; // ör. DDC yazma sınırı
        rig.BlueLight.Applied = false;
        rig.Nvidia.SetResult = false;
        rig.Tone.Result = false;

        var result = rig.Profiles.Apply(User("Hepsi", 0.5, 4000, 70) with { Tone = ToneStyle.Contrast });

        Assert.Equal([Brightness(0.5), "kelvin:4000", "tone:Contrast", "vibrance:70"], rig.Log); // biri bozulunca ötekiler atlanmaz
        Assert.Equal(
            new ProfileApplyResult("Hepsi", ProfileStep.Failed, ProfileStep.Failed, ProfileStep.Failed, ProfileStep.Failed), result);
        Assert.False(result.Success);
    }

    /// <summary>Sihirbaz sırasında mavi ışık askıdadır: seçim saklanır, sihirbaz bitince gelir; başarısızlık sayılmaz.</summary>
    [Fact]
    public void Apply_WhileBlueLightSuspended_ReportsDeferred_NotFailed()
    {
        var rig = new Rig();
        rig.BlueLight.Suspended = true;
        rig.BlueLight.Applied = false; // askıdayken filtre Applied=false döner

        var result = rig.Profiles.Apply(rig.Built("Akşam"));

        Assert.Equal(ProfileStep.Deferred, result.BlueLight);
        Assert.True(result.Success);
    }

    // ---- Geçersiz profil: hiçbir şey yazılmaz ----

    [Theory]
    [InlineData(-0.1, 4000, 70)]
    [InlineData(1.1, 4000, 70)]
    [InlineData(double.NaN, 4000, 70)]
    [InlineData(0.5, 1899, 70)]
    [InlineData(0.5, 6501, 70)]
    [InlineData(0.5, 4000, 49)]
    [InlineData(0.5, 4000, 101)]
    public void Apply_InvalidProfile_Throws_AndWritesNothing(double brightness, int kelvin, int vibrance)
    {
        var rig = new Rig();

        Assert.Throws<ArgumentException>(() => rig.Profiles.Apply(User("Kötü", brightness, kelvin, vibrance)));

        Assert.Empty(rig.Log);
    }

    // ---- Kullanıcı profilleri ----

    [Fact]
    public void Save_AddsUserProfile_AfterBuiltIns_AndSurvivesReopen()
    {
        var rig = new Rig();

        rig.Profiles.Save(User("Okuma", 0.4, 4500));
        rig.Reopen();

        var last = rig.Profiles.Profiles[^1];
        Assert.Equal(new ColorProfile("Okuma", false, 0.4, 4500, null), last);
        Assert.Equal(BuiltInNames.Length + 1, rig.Profiles.Profiles.Count);
    }

    [Fact]
    public void Save_SameName_ReplacesInPlace_CaseInsensitive()
    {
        var rig = new Rig();
        rig.Profiles.Save(User("Okuma", 0.4, 4500));
        rig.Profiles.Save(User("Başka", 0.3, 3000));

        rig.Profiles.Save(User("okuma", 0.6, 5000));

        var users = rig.Profiles.Profiles.Where(p => !p.BuiltIn).ToArray();
        Assert.Equal(["okuma", "Başka"], users.Select(p => p.Name));
        Assert.Equal(0.6, users[0].Brightness);
    }

    /// <summary>Kullanıcı bir profili "hazır" diye işaretleyemez; hazır adlar ezilemez.</summary>
    [Theory]
    [InlineData("Gündüz")]
    [InlineData("gündüz")]
    [InlineData("  Film  ")]
    [InlineData("DaVinci zaman çizelgesi")]
    public void Save_BuiltInName_Throws(string name)
    {
        var rig = new Rig();

        Assert.Throws<ArgumentException>(() => rig.Profiles.Save(User(name)));

        Assert.Equal(BuiltInNames, rig.Profiles.Profiles.Select(p => p.Name));
        Assert.Empty(rig.Log);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Save_EmptyName_Throws(string name) => Assert.Throws<ArgumentException>(() => new Rig().Profiles.Save(User(name)));

    [Fact]
    public void Save_TooLongName_Throws() =>
        Assert.Throws<ArgumentException>(() => new Rig().Profiles.Save(User(new string('a', 41))));

    [Fact]
    public void Save_TrimsName_AndForcesBuiltInFalse()
    {
        var rig = new Rig();

        rig.Profiles.Save(new ColorProfile("  Okuma  ", BuiltIn: true, 0.4, 4500, null));

        var saved = rig.Profiles.Profiles[^1];
        Assert.Equal("Okuma", saved.Name);
        Assert.False(saved.BuiltIn);
    }

    [Fact]
    public void Save_InvalidValues_Throws_AndStoresNothing()
    {
        var rig = new Rig();

        Assert.Throws<ArgumentException>(() => rig.Profiles.Save(User("Kötü", vibrance: 200)));

        Assert.Empty(rig.Log);
        Assert.Equal(BuiltInNames.Length, rig.Profiles.Profiles.Count);
    }

    [Fact]
    public void Delete_RemovesUserProfile()
    {
        var rig = new Rig();
        rig.Profiles.Save(User("Okuma"));

        rig.Profiles.Delete("okuma");
        rig.Reopen();

        Assert.Equal(BuiltInNames, rig.Profiles.Profiles.Select(p => p.Name));
    }

    [Fact]
    public void Delete_BuiltIn_Throws_AndDelete_Unknown_IsNoOp()
    {
        var rig = new Rig();

        Assert.Throws<InvalidOperationException>(() => rig.Profiles.Delete("Film"));
        rig.Profiles.Delete("yok böyle bir profil");

        Assert.Equal(BuiltInNames, rig.Profiles.Profiles.Select(p => p.Name));
    }

    // ---- Bozuk ya da elle değiştirilmiş depo ----

    /// <summary>Depoda hazır bir adla kayıt varsa hazır profil kazanır (değerleri değişmez).</summary>
    [Fact]
    public void Store_EntryWithBuiltInName_IsIgnored()
    {
        var rig = new Rig();
        rig.Store.Sections[ColorProfiles.Section] = new ColorProfilesRecord(
            [new ColorProfile("Gündüz", false, 0.0, 1900, 100), User("Geçerli")]);

        var profiles = rig.Reopen().Profiles;

        Assert.Equal(BuiltInNames.Append("Geçerli"), profiles.Select(p => p.Name));
        Assert.NotEqual(0.0, profiles[0].Brightness);
    }

    /// <summary>Geçersiz değerli kayıt (elle bozulmuş dosya) listeden düşer; uygulanamaz.</summary>
    [Fact]
    public void Store_EntryWithInvalidValues_IsDropped()
    {
        var rig = new Rig();
        rig.Store.Sections[ColorProfiles.Section] = new ColorProfilesRecord(
            [User("Kötü", 5.0, 4000), User("Geçerli"), new ColorProfile(null!, false, 0.5, 4000, null)]);

        var names = rig.Reopen().Profiles.Select(p => p.Name);

        Assert.Equal(BuiltInNames.Append("Geçerli"), names);
    }

    // ---- Panik (T310 arayüzü bağlar; servis tarafı) ----

    /// <summary>Panik: ek karartma, mavi ışık, ton eğrisi (T320) ve canlılık kapanır. Kalibrasyona dokunacak bir bağımlılık yok.</summary>
    [Fact]
    public void PanicOff_TurnsOffDimmingBlueLightToneAndVibrance_Only()
    {
        var rig = new Rig();
        rig.Profiles.Apply(rig.Built("Film"));
        rig.Log.Clear();

        rig.Profiles.PanicOff();

        Assert.Equal(["dimming:off", "bluelight:off", "tone:off", "vibrance:reset"], rig.Log);
        Assert.Equal(ToneStyle.Standard, rig.Tone.Style);
        Assert.Equal(KelvinCalculator.OffKelvin, rig.BlueLight.Kelvin);
        Assert.Null(rig.Nvidia.Vibrance);
    }

    /// <summary>Biri atarsa ötekiler yine de kapanır.</summary>
    [Fact]
    public void PanicOff_StillTurnsOffTheRest_WhenOneThrows()
    {
        var log = new List<string>();
        var profiles = new ColorProfiles(
            new ThrowingBrightness(), new FakeBlueLight(log), new FakeNvidia(log), new FakeTone(log), new FakeStore(log), Key);

        Assert.Throws<InvalidOperationException>(profiles.PanicOff);

        Assert.Equal(["bluelight:off", "tone:off", "vibrance:reset"], log);
    }

    private sealed class ThrowingBrightness : IBrightnessControl
    {
        public bool DimmingActive => true;
        public DimmingPlan Preview(double slider) => throw new NotSupportedException();
        public BrightnessState Commit(double slider) => throw new NotSupportedException();
        public double? ReadSlider() => throw new NotSupportedException();
        public bool DisableDimming() => throw new InvalidOperationException("GPU tablosu yazılamadı");
        public bool Suspend() => throw new NotSupportedException();
        public bool Resume() => throw new NotSupportedException();
        public bool ResumeAt(double slider) => throw new NotSupportedException();
        public bool Shutdown() => throw new NotSupportedException();
    }
}
