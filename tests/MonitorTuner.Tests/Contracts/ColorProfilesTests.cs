using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Core.Profiles;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;
using MonitorTuner.Services;
using MonitorTuner.Tests.Fakes;

namespace MonitorTuner.Tests.Contracts;

/// <summary>
/// Renk profilleri sözleşmesi (02-MIMARI.md, T308): sahte sistem, gerçek Güvenlik, DDC, renk boru hattı, parlaklık,
/// mavi ışık ve NVIDIA servisleriyle uçtan uca. Profiller Hz'e asla dokunmaz ve sadece kullanıcı seçince uygulanır.
/// </summary>
public class ColorProfilesTests
{
    private const byte Backlight = 0x10, RedGain = 0x16, GreenGain = 0x18, BlueGain = 0x1A, VerticalFrequency = 0xAE;
    private static readonly DisplayId Display = FakeHardware.Display;
    private static readonly MonitorKey Key = new("HP P21b G4", "SN1", "HDMI", "RTX 3080");
    private static readonly DisplayMode Mode76 = new(1920, 1080, 76);

    private static readonly MonitorProfile Profile =
        new MonitorProfileReader().Read(File.ReadAllText(Path.Combine(RepoRoot.Find(), "profiles", "hp-p21b-g4.json")));

    /// <summary>
    /// Her hazır profil sahte donanımda doğru değerleri yazıyor; Hz (Windows modu, kayıtlı mod, monitörün AE'si) hiç
    /// değişmiyor ve hiçbir ekran modu geçişi/kaydı olmuyor.
    /// </summary>
    [Fact]
    public void Apply_SetsValuesOnce_AndNeverChangesRefreshRate()
    {
        var rig = new Rig();
        rig.UserChoosesMode76();
        var hz = rig.HzSnapshot();

        foreach (var profile in rig.Profiles.Profiles)
        {
            rig.Hw.Clock.Advance(TimeSpan.FromMinutes(2)); // DDC yazma sınırı: aynı koda dakikada en çok 10 ve 1 sn arayla

            var result = rig.Profiles.Apply(profile);

            Assert.True(result.Success, $"{profile.Name}: {result}");
            AssertValuesWritten(rig, profile);
            Assert.Equal(hz, rig.HzSnapshot()); // her profilden sonra Hz aynı
        }

        Assert.Equal(Mode76, rig.Hw.Modes.Current);
        Assert.Single(rig.Hw.Modes.Switches); // yalnızca kullanıcının kendi 76 Hz seçimi
        Assert.Equal(1, rig.Hw.Modes.PersistCount);
    }

    /// <summary>Kurulum, listeleme, kaydetme, silme, yeniden açılış ve zamanın akması: donanıma hiçbir şey yazılmaz.</summary>
    [Fact]
    public void NothingIsWritten_UntilTheUserSelectsAProfile()
    {
        var rig = new Rig();
        rig.UserChoosesMode76();
        var hz = rig.HzSnapshot();

        _ = rig.Profiles.Profiles;
        rig.Profiles.Save(new ColorProfile("Benim", false, 0.5, 4000, 60));
        rig.Profiles.Delete("Benim");
        rig.Hw.Clock.Advance(TimeSpan.FromDays(1));
        rig.BlueLight.Tick(); // akşam geçişi kapalı: kendiliğinden bir şey değişmez

        Assert.Empty(rig.Hw.Monitor.Writes);
        Assert.Empty(rig.Hw.Gpu.Writes);
        Assert.Equal(0, rig.Hw.Nvidia.WriteCount);
        Assert.Equal(0, rig.Hw.GammaRange.WriteCount);
        Assert.Equal(hz, rig.HzSnapshot());
    }

    /// <summary>Kullanıcı profili de Hz'e dokunmaz; yalnız seçilen profilin ayarları değişir.</summary>
    [Fact]
    public void Apply_UserProfile_WritesOnlyItsSettings_AndLeavesHzAlone()
    {
        var rig = new Rig();
        rig.UserChoosesMode76();
        var hz = rig.HzSnapshot();
        var contrastBefore = rig.Hw.Monitor.Read(Display, 0x12);

        var result = rig.Profiles.Apply(new ColorProfile("Sadece canlılık", false, null, null, 70));

        Assert.True(result.Success);
        Assert.Empty(rig.Hw.Monitor.Writes); // arka ışığa ve kazançlara dokunulmadı
        Assert.Equal(70, rig.Hw.Nvidia.Read(Display)!.Value.Vibrance);
        Assert.Equal(contrastBefore, rig.Hw.Monitor.Read(Display, 0x12));
        Assert.Equal(hz, rig.HzSnapshot());
    }

    /// <summary>Film'den Gündüz'e geçiş canlılığı ve mavi ışığı kapatır; ek karartma açıkken DaVinci karartmayı da kaldırır.</summary>
    [Fact]
    public void Apply_NeutralProfile_ClearsEarlierEffects()
    {
        var rig = new Rig();
        var initialGains = new[] { RedGain, GreenGain, BlueGain }.Select(c => rig.Hw.Monitor.Read(Display, c)!.Value.Current).ToArray();

        Assert.True(rig.Brightness.Commit(0.1).Applied); // kullanıcı ek karartmadaydı
        Assert.True(rig.Brightness.DimmingActive);
        rig.Hw.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.True(rig.Profiles.Apply(rig.Built("Film")).Success);
        Assert.Equal(rig.Built("Film").Vibrance, rig.Hw.Nvidia.Read(Display)!.Value.Vibrance);
        rig.Hw.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.True(rig.Profiles.Apply(rig.Built("Gece")).Success);
        Assert.Equal(FakeNvidiaSettings.DriverDefault.Vibrance, rig.Hw.Nvidia.Read(Display)!.Value.Vibrance); // Film'in canlılığı Gece'de kapandı

        rig.Hw.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.True(rig.Profiles.Apply(rig.Built("DaVinci zaman çizelgesi")).Success);

        Assert.False(rig.Brightness.DimmingActive);
        Assert.Equal(KelvinCalculator.OffKelvin, rig.BlueLight.Kelvin);
        Assert.Equal(FakeNvidiaSettings.DriverDefault.Vibrance, rig.Hw.Nvidia.Read(Display)!.Value.Vibrance);
        Assert.Equal(
            initialGains,
            new[] { RedGain, GreenGain, BlueGain }.Select(c => rig.Hw.Monitor.Read(Display, c)!.Value.Current).ToArray());
        Assert.Equal(GammaRamp.Identity, rig.Hw.Gpu.Read(Display)); // tabloda efekt kalmadı
    }

    /// <summary>Panik kısayolu (T310 bağlar): bütün efektler kapanır.</summary>
    [Pending("T310")]
    public void PanicOff_TurnsOffEffects_KeepsCalibration() => Assert.Fail("T310");

    // ---- Doğrulama ----

    private static void AssertValuesWritten(Rig rig, ColorProfile profile)
    {
        if (profile.Brightness is { } slider)
        {
            var plan = new DimmingPlanner().Plan(slider);
            var backlight = rig.Hw.Monitor.Read(Display, Backlight)!.Value;
            Assert.Equal((int)Math.Round(plan.Backlight / 100.0 * backlight.Maximum, MidpointRounding.AwayFromZero), backlight.Current);
            Assert.Equal(plan.GpuScale < 1, rig.Brightness.DimmingActive);
        }

        if (profile.Kelvin is { } kelvin) Assert.Equal(kelvin, rig.BlueLight.Kelvin);

        // Kullanıcının değeri sürücü varsayılanı (%50): profilde canlılık yoksa oraya dönülür.
        Assert.Equal(profile.Vibrance ?? FakeNvidiaSettings.DriverDefault.Vibrance, rig.Hw.Nvidia.Read(Display)!.Value.Vibrance);
    }

    // ---- Kurulum ----

    private sealed record HzState(DisplayMode Current, DisplayMode Persisted, int Switches, int Persists, DdcValue? Readout);

    /// <summary>Sahte sistem (76 Hz modu eklenmiş) + gerçek servisler; ColorProfiles'ın tüm bağımlılıkları gerçek.</summary>
    private sealed class Rig
    {
        public Rig()
        {
            var backupStore = new MemoryBackupStore();
            Hw = new FakeHardware(76, 75, 72);
            Safety = new SafetyLayer(
                Display, [.. Profile.WritableDdcCodes], new SafetyHardware(Hw.Monitor, Hw.Gpu, Hw.Modes, Hw.Nvidia, Hw.GammaRange),
                () => Pipeline!, backupStore, Hw.Clock);
            Nvidia = new NvidiaControl(Display, Hw.Nvidia, Safety, backupStore);
            Pipeline = new ColorPipeline(Display, Hw.Gpu, Hw.GammaRange, new GammaTableCombiner(), Safety, Nvidia);
            Ddc = new DdcControl(Display, Hw.Monitor, Safety, Profile, Hw.Clock);
            Brightness = new BrightnessControl(Ddc, Pipeline, new DimmingPlanner());
            BlueLight = new BlueLightFilter(Profile, Ddc, Pipeline, new KelvinCalculator(), new NoConflicts(), Hw.Clock, Settings, Key);
            Profiles = new ColorProfiles(Brightness, BlueLight, Nvidia, Settings, Key);
        }

        public FakeHardware Hw { get; }
        public SafetyLayer Safety { get; }
        public NvidiaControl Nvidia { get; }
        public ColorPipeline Pipeline { get; }
        public DdcControl Ddc { get; }
        public BrightnessControl Brightness { get; }
        public BlueLightFilter BlueLight { get; }
        public ColorProfiles Profiles { get; }
        public MemorySettings Settings { get; } = new();

        public ColorProfile Built(string name) => Profiles.Profiles.Single(p => p.Name == name);

        /// <summary>Kullanıcının kendi Hz seçimi (NVIDIA'da modu ekledi, Hz panelinden 76'ya geçti ve onayladı).</summary>
        public void UserChoosesMode76()
        {
            var point = FakeRestorePoints.For(WriteTarget.DisplayMode, Display);
            Assert.True(Hw.Modes.SwitchMode(Display, Mode76, point));
            Assert.True(Hw.Modes.PersistMode(Display, Mode76, point));
        }

        public HzState HzSnapshot() => new(
            Hw.Modes.Current, Hw.Modes.Persisted, Hw.Modes.Switches.Count, Hw.Modes.PersistCount,
            Hw.Monitor.Read(Display, VerticalFrequency));
    }

    private sealed class NoConflicts : IColorConflictProbe
    {
        public IReadOnlyList<ColorConflict> Detect() => [];
    }

    private sealed class MemorySettings : ISettingsStore
    {
        private readonly Dictionary<string, object> sections = [];
        public string BackupFolder => "";

        public T? Read<T>(MonitorKey monitor, string section) =>
            sections.TryGetValue(section, out var v) && v is T t ? t : default;

        public void Write<T>(MonitorKey monitor, string section, T value) => sections[section] = value!;
    }

    private sealed class MemoryBackupStore : IFactoryBackupStore
    {
        private FactoryBackup? saved;
        public FactoryBackup? Load() => saved;
        public void Save(FactoryBackup backup) => saved = backup;
    }
}
