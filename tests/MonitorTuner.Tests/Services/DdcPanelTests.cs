using Microsoft.Extensions.Time.Testing;
using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Core.Profiles;
using MonitorTuner.Services;
using MonitorTuner.Tests.Fakes;

namespace MonitorTuner.Tests.Services;

/// <summary>
/// DDC paneli (T305): parlaklık, kontrast, renk ön ayarı, RGB kazancı, keskinlik, giriş, hoparlör sesi. Sahte monitör
/// (kullanıcının ünitesinden kaydedilen değerler) üzerinden. Ayar sahipliği: OSD'den değişen değer ezilmez; RGB kazancı
/// T307'nin mavi ışık filtresiyle paylaşılır.
/// </summary>
public class DdcPanelTests
{
    private static readonly MonitorProfile Profile =
        new MonitorProfileReader().Read(File.ReadAllText(Path.Combine(RepoRoot.Find(), "profiles", "hp-p21b-g4.json")));

    private static readonly MonitorKey Key = new("HP P21b G4", "SN1", "HDMI", "RTX 3080");
    private const byte Brightness = 0x10, Contrast = 0x12, Preset = 0x14, Red = 0x16, Green = 0x18, Blue = 0x1A;
    private const byte InputCode = 0x60, Volume = 0x62, Sharpness = 0x87;
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
        public int Kelvin { get; set; } = 6500;
        public bool IsSuspended { get; set; }
        public EveningSettings Evening { get; set; } = EveningSettings.Default;
        public BlueLightState SetKelvin(int kelvin) => throw new NotSupportedException();
        public BlueLightState Disable() => throw new NotSupportedException();
        public BlueLightState Suspend() => throw new NotSupportedException();
        public BlueLightState Resume() => throw new NotSupportedException();
        public BlueLightState Shutdown() => throw new NotSupportedException();
        public LowBlueLightResult SelectMonitorLowBlueLight() => throw new NotSupportedException();
        public IReadOnlyList<ColorConflict> CheckConflicts() => [];
        public bool Tick() => false;
    }

    private sealed class Rig
    {
        public Rig(FakeDdcMonitor? monitor = null, MonitorProfile? profile = null, ConnectionType connection = ConnectionType.Hdmi)
        {
            Monitor = monitor ?? FakeDdcMonitor.HpP21bG4();
            Profile = profile ?? DdcPanelTests.Profile;
            Ddc = new DirectDdcControl(Monitor, Profile);
            Connection = connection;
            Panel = NewPanel();
        }

        public FakeDdcMonitor Monitor { get; }
        public MonitorProfile Profile { get; }
        public DirectDdcControl Ddc { get; }
        public StubBlueLight BlueLight { get; } = new();
        public FakeStore Store { get; } = new();
        public ConnectionType Connection { get; }
        public DdcPanel Panel { get; }
        public bool Dimming { get; set; }

        /// <summary>Aynı monitör ve depoyla yeni örnek: uygulama yeniden açıldı.</summary>
        public DdcPanel NewPanel() => new(Profile, Ddc, BlueLight, Store, Key, Connection, () => Dimming);

        public PanelSettingState State(PanelSetting setting) => Panel.Refresh().Settings.Single(s => s.Setting == setting);

        public void UseCustomRgb() => Monitor.SetMonitorValue(Preset, CustomRgb);
    }

    // ---- Okuma ----

    [Fact]
    public void Refresh_ListsSevenSettings_WithRecordedValuesAndRanges()
    {
        var state = new Rig().Panel.Refresh();

        Assert.True(state.Available);
        Assert.Empty(state.ExternallyChanged);
        var by = state.Settings.ToDictionary(s => s.Setting);
        Assert.Equal(9, by.Count); // 7 başlık, RGB üç kanal
        Assert.Equal((55, 0, 100), (by[PanelSetting.Brightness].Current, by[PanelSetting.Brightness].Minimum, by[PanelSetting.Brightness].Maximum));
        Assert.Equal(72, by[PanelSetting.Contrast].Current);
        Assert.Equal(5, by[PanelSetting.ColorPreset].Current);
        Assert.Equal((4, 1, 7), (by[PanelSetting.Sharpness].Current, by[PanelSetting.Sharpness].Minimum, by[PanelSetting.Sharpness].Maximum));
        Assert.Equal(17, by[PanelSetting.Input].Current);
        Assert.Equal((50, 0, 100), (by[PanelSetting.Volume].Current, by[PanelSetting.Volume].Minimum, by[PanelSetting.Volume].Maximum));
        Assert.All([PanelSetting.RedGain, PanelSetting.GreenGain, PanelSetting.BlueGain], g => Assert.Equal(255, by[g].Current));
        Assert.Equal(Brightness, by[PanelSetting.Brightness].Code);
    }

    [Fact]
    public void Refresh_ReadsOnly_NeverWrites()
    {
        var rig = new Rig();

        rig.Panel.Refresh();
        rig.Panel.Refresh();

        Assert.Empty(rig.Monitor.Writes);
    }

    [Fact]
    public void NoDdc_ReportsUnavailable_AndWritesNothing()
    {
        var monitor = FakeDdcMonitor.WithoutDdc();
        var rig = new Rig(monitor);

        var state = rig.Panel.Refresh();

        Assert.False(state.Available);
        Assert.Empty(state.Settings);
        Assert.Equal(PanelSetResult.NoDdc, rig.Panel.Set(PanelSetting.Brightness, 40));
        Assert.Empty(monitor.Writes);
    }

    [Fact]
    public void DdcNotResponding_IsUnavailable_SoUserGetsOsdInstructions()
    {
        var rig = new Rig();
        rig.Monitor.Responding = false;

        Assert.False(rig.Panel.Refresh().Available);
    }

    // ---- Yazma: sınırlar ----

    [Fact]
    public void Set_Brightness_WritesOnce_AndSameValueWritesNothing()
    {
        var rig = new Rig();

        Assert.Equal(PanelSetResult.Written, rig.Panel.Set(PanelSetting.Brightness, 40));
        Assert.Equal(PanelSetResult.Unchanged, rig.Panel.Set(PanelSetting.Brightness, 40));

        Assert.Equal(1, rig.Monitor.WriteCount(Brightness));
        Assert.Equal(40, rig.State(PanelSetting.Brightness).Current);
    }

    [Theory]
    [InlineData(PanelSetting.Brightness, -1)]
    [InlineData(PanelSetting.Brightness, 101)]
    [InlineData(PanelSetting.Sharpness, 0)]
    [InlineData(PanelSetting.Sharpness, 8)]
    [InlineData(PanelSetting.Volume, 101)]
    [InlineData(PanelSetting.Volume, -5)]
    public void Set_OutOfRange_IsRejected_WithoutWriting(PanelSetting setting, int value)
    {
        var rig = new Rig();

        Assert.Equal(PanelSetResult.OutOfRange, rig.Panel.Set(setting, value));
        Assert.Empty(rig.Monitor.Writes);
    }

    [Theory]
    [InlineData(PanelSetting.Brightness, 0, Brightness)] // PWM yok: arka ışık 0'a kadar serbest (08-GORUNTU-YONTEMLERI.md → 3)
    [InlineData(PanelSetting.Brightness, 100, Brightness)]
    [InlineData(PanelSetting.Sharpness, 1, Sharpness)]
    [InlineData(PanelSetting.Sharpness, 7, Sharpness)]
    [InlineData(PanelSetting.Volume, 0, Volume)]
    [InlineData(PanelSetting.Volume, 100, Volume)]
    public void Set_Boundaries_AreAllowed(PanelSetting setting, int value, byte code)
    {
        var rig = new Rig();

        Assert.Equal(PanelSetResult.Written, rig.Panel.Set(setting, value));
        Assert.Equal(value, rig.Monitor.Read(FakeHardware.Display, code)!.Value.Current);
    }

    // ---- Dijital parlaklık koruması ----

    [Fact]
    public void Contrast_CannotGoAboveStartingValue_DigitalBrightnessProtection()
    {
        var rig = new Rig();

        Assert.Equal(PanelSetResult.BlockedDigitalBoost, rig.Panel.Set(PanelSetting.Contrast, 73));
        Assert.Equal(PanelSetResult.BlockedDigitalBoost, rig.Panel.Set(PanelSetting.Contrast, 100));
        Assert.Equal(0, rig.Monitor.WriteCount(Contrast));
        Assert.Equal(72, rig.State(PanelSetting.Contrast).Maximum);
    }

    [Fact]
    public void Contrast_LoweredThenRestored_UpToTheCeiling()
    {
        var rig = new Rig();

        Assert.Equal(PanelSetResult.Written, rig.Panel.Set(PanelSetting.Contrast, 60));
        Assert.Equal(PanelSetResult.Written, rig.Panel.Set(PanelSetting.Contrast, 72));
        Assert.Equal(PanelSetResult.BlockedDigitalBoost, rig.Panel.Set(PanelSetting.Contrast, 73));
    }

    [Fact]
    public void Contrast_CeilingSurvivesRestart_NotResetToLoweredValue()
    {
        var rig = new Rig();
        rig.Panel.Set(PanelSetting.Contrast, 50);

        var reopened = rig.NewPanel();

        Assert.Equal(72, reopened.Refresh().Settings.Single(s => s.Setting == PanelSetting.Contrast).Maximum);
        Assert.Equal(PanelSetResult.Written, reopened.Set(PanelSetting.Contrast, 72));
    }

    [Fact]
    public void Contrast_RaisedInOsd_IsUsersChoice_AndRaisesCeiling()
    {
        var rig = new Rig();
        rig.Panel.Refresh();
        rig.Monitor.SetMonitorValue(Contrast, 80); // kullanıcı OSD'den yükseltti

        var state = rig.Panel.Refresh();

        Assert.Equal([PanelSetting.Contrast], state.ExternallyChanged);
        Assert.Equal(80, state.Settings.Single(s => s.Setting == PanelSetting.Contrast).Maximum);
        Assert.Equal(80, rig.NewPanel().Refresh().Settings.Single(s => s.Setting == PanelSetting.Contrast).Maximum);
    }

    [Fact]
    public void Contrast_HasAFloor_SoScreenNeverGoesBlack()
    {
        var rig = new Rig();

        Assert.Equal(PanelSetResult.OutOfRange, rig.Panel.Set(PanelSetting.Contrast, 0));
        Assert.Equal(PanelSetResult.OutOfRange, rig.Panel.Set(PanelSetting.Contrast, 14));
        Assert.Equal(PanelSetResult.Written, rig.Panel.Set(PanelSetting.Contrast, 15)); // SafetyLimits.MinWhiteLevel
    }

    // ---- Ayar sahipliği: dışarıdan değişen değer ezilmez ----

    [Fact]
    public void Refresh_ReportsExternalChange_Once_AndAdoptsIt()
    {
        var rig = new Rig();
        rig.Panel.Refresh();
        rig.Monitor.SetMonitorValue(Brightness, 30);

        var first = rig.Panel.Refresh();
        var second = rig.Panel.Refresh();

        Assert.Equal([PanelSetting.Brightness], first.ExternallyChanged);
        Assert.Equal(30, first.Settings.Single(s => s.Setting == PanelSetting.Brightness).Current);
        Assert.Empty(second.ExternallyChanged);
    }

    [Fact]
    public void Set_AfterExternalChange_DoesNotOverwrite_UntilUserSeesIt()
    {
        var rig = new Rig();
        rig.Panel.Refresh(); // kullanıcı paneli 55 görüyor
        rig.Monitor.SetMonitorValue(Brightness, 30); // OSD tuşu

        Assert.Equal(PanelSetResult.ExternallyChanged, rig.Panel.Set(PanelSetting.Brightness, 70));
        Assert.Equal(0, rig.Monitor.WriteCount(Brightness));
        Assert.Equal(30, rig.Monitor.Read(FakeHardware.Display, Brightness)!.Value.Current);

        // Kullanıcı yeni değeri gördü ve yine ister: şimdi yazılır.
        Assert.Equal(PanelSetResult.Written, rig.Panel.Set(PanelSetting.Brightness, 70));
        Assert.Equal(1, rig.Monitor.WriteCount(Brightness));
    }

    [Fact]
    public void Set_OtherSettingsExternalChange_DoesNotBlockThisOne()
    {
        var rig = new Rig();
        rig.Panel.Refresh();
        rig.Monitor.SetMonitorValue(Volume, 10);

        Assert.Equal(PanelSetResult.Written, rig.Panel.Set(PanelSetting.Brightness, 70));
        Assert.Equal(10, rig.Monitor.Read(FakeHardware.Display, Volume)!.Value.Current);
    }

    [Fact]
    public void Set_WithoutPriorRefresh_Works()
    {
        var rig = new Rig();

        Assert.Equal(PanelSetResult.Written, rig.Panel.Set(PanelSetting.Brightness, 70));
    }

    // ---- Parlaklık sahipliği: ek karartma (T306) ----

    [Fact]
    public void Brightness_WhileDimmingIsActive_IsLocked_AndOsdChangesAreNotCountedAsExternal()
    {
        var rig = new Rig();
        rig.Panel.Refresh();

        // T306 kaydırıcısı karartma bölgesine girdi ve arka ışığı kendisi indirdi.
        rig.Dimming = true;
        rig.Monitor.SetMonitorValue(Brightness, 0);
        var state = rig.Panel.Refresh();

        Assert.Empty(state.ExternallyChanged);
        Assert.Equal(SettingLock.DimmingActive, state.Settings.Single(s => s.Setting == PanelSetting.Brightness).Lock);
        Assert.Equal(PanelSetResult.Locked, rig.Panel.Set(PanelSetting.Brightness, 60));
        Assert.Equal(0, rig.Monitor.WriteCount(Brightness));

        // Karartma kapandı: panel yine yazabilir.
        rig.Dimming = false;
        Assert.Empty(rig.Panel.Refresh().ExternallyChanged);
        Assert.Equal(PanelSetResult.Written, rig.Panel.Set(PanelSetting.Brightness, 60));
    }

    // ---- Renk ön ayarı ----

    [Fact]
    public void ColorPreset_OnlyConfirmedMappingsAreSelectable()
    {
        var choices = new Rig().State(PanelSetting.ColorPreset).Choices;

        Assert.Equal([2, 4, 5, 8, 11, 12, 13, 14, 15], choices.Select(c => c.Value));
        Assert.Equal([5], choices.Where(c => c.Selectable).Select(c => c.Value));
        Assert.Equal("Neutral", choices.Single(c => c.Value == 5).Name);
        Assert.Equal("CustomRgb", choices.Single(c => c.Value == CustomRgb).Name);
    }

    [Fact]
    public void ColorPreset_EstimatedMapping_IsNeverWritten()
    {
        var rig = new Rig();

        Assert.Equal(PanelSetResult.NotConfirmed, rig.Panel.Set(PanelSetting.ColorPreset, 4)); // Warm: tahmin
        Assert.Equal(PanelSetResult.NotConfirmed, rig.Panel.Set(PanelSetting.ColorPreset, CustomRgb));
        Assert.Equal(PanelSetResult.OutOfRange, rig.Panel.Set(PanelSetting.ColorPreset, 99));
        Assert.Equal(0, rig.Monitor.WriteCount(Preset));
    }

    [Fact]
    public void ColorPreset_ConfirmedMapping_IsWritten()
    {
        var profile = Profile with
        {
            ColorPresetMap = [.. Profile.ColorPresetMap.Select(m => m.DdcValue == 4 ? m with { Confidence = MappingConfidence.Confirmed } : m)],
        };
        var rig = new Rig(profile: profile);

        Assert.Equal(PanelSetResult.Written, rig.Panel.Set(PanelSetting.ColorPreset, 4));
        Assert.Equal(4, rig.Monitor.Read(FakeHardware.Display, Preset)!.Value.Current);
    }

    // ---- RGB kazancı ----

    [Fact]
    public void Gain_InNeutralPreset_IsLocked_BecauseItHasNoEffect()
    {
        var rig = new Rig();

        var gain = rig.State(PanelSetting.RedGain);

        Assert.Equal(SettingLock.WrongColorPreset, gain.Lock);
        Assert.False(gain.Editable);
        Assert.Equal(PanelSetResult.Locked, rig.Panel.Set(PanelSetting.RedGain, 240));
        Assert.Empty(rig.Monitor.Writes);
    }

    [Fact]
    public void Gain_InCustomRgb_ReducesOnly_WithAFloor()
    {
        var rig = new Rig();
        rig.UseCustomRgb();

        var gain = rig.State(PanelSetting.BlueGain);
        Assert.True(gain.Editable);
        Assert.Equal((153, 255), (gain.Minimum, gain.Maximum)); // en düşük %60 (T307 ile aynı taban)

        Assert.Equal(PanelSetResult.Written, rig.Panel.Set(PanelSetting.BlueGain, 230));
        Assert.Equal(PanelSetResult.OutOfRange, rig.Panel.Set(PanelSetting.BlueGain, 256));
        Assert.Equal(PanelSetResult.OutOfRange, rig.Panel.Set(PanelSetting.BlueGain, 152));
        Assert.Equal(PanelSetResult.OutOfRange, rig.Panel.Set(PanelSetting.BlueGain, 0));
        Assert.Equal(230, rig.Monitor.Read(FakeHardware.Display, Blue)!.Value.Current);
        Assert.Equal(1, rig.Monitor.WriteCount(Blue));
    }

    [Fact]
    public void Gain_WhileBlueLightFilterIsActive_IsLocked_AndFilterChangesAreNotExternal()
    {
        var rig = new Rig();
        rig.UseCustomRgb();
        rig.Panel.Refresh();

        // Filtre açıldı ve kazancı kendisi düşürdü: bu, kullanıcının OSD değişikliği değil.
        rig.BlueLight.Kelvin = 3400;
        rig.Monitor.SetMonitorValue(Blue, 150);
        var state = rig.Panel.Refresh();

        Assert.Empty(state.ExternallyChanged);
        Assert.Equal(SettingLock.BlueLightActive, state.Settings.Single(s => s.Setting == PanelSetting.BlueGain).Lock);
        Assert.Equal(PanelSetResult.Locked, rig.Panel.Set(PanelSetting.BlueGain, 200));

        // Filtre kapandı, kazanç eski değerine döndü: yine dış değişiklik sayılmaz.
        rig.BlueLight.Kelvin = 6500;
        rig.Monitor.SetMonitorValue(Blue, 255);
        Assert.Empty(rig.Panel.Refresh().ExternallyChanged);
        Assert.Equal(PanelSetResult.Written, rig.Panel.Set(PanelSetting.BlueGain, 200));
    }

    [Fact]
    public void Gain_WhileBlueLightIsSuspended_IsEditable()
    {
        var rig = new Rig();
        rig.UseCustomRgb();
        rig.BlueLight.Kelvin = 3400;
        rig.BlueLight.IsSuspended = true; // ayar sihirbazı: efektler geçici kapalı

        Assert.Equal(PanelSetResult.Written, rig.Panel.Set(PanelSetting.RedGain, 240));
    }

    [Fact]
    public void Gain_BlueLightFilter_DoesNotOverwriteUsersValue_AndViceVersa()
    {
        // Gerçek filtre + panel + sahte monitör: iki taraf aynı 16/18/1A kodlarını paylaşıyor.
        var rig = new Rig();
        rig.UseCustomRgb();
        var filter = new BlueLightFilter(
            Profile, rig.Ddc, new NoOpPipeline(), new KelvinCalculator(), new NoConflicts(),
            new FakeTimeProvider(FakeHardware.Start), rig.Store, Key);
        var panel = new DdcPanel(Profile, rig.Ddc, filter, rig.Store, Key, ConnectionType.Hdmi);

        // Kullanıcı mavi kazancı 230'a çekti (T305), sonra filtreyi açtı (T307).
        Assert.Equal(PanelSetResult.Written, panel.Set(PanelSetting.BlueGain, 230));
        filter.SetKelvin(3400);
        Assert.True(rig.Monitor.Read(FakeHardware.Display, Blue)!.Value.Current < 230);
        var locked = panel.Refresh();
        Assert.Equal(SettingLock.BlueLightActive, locked.Settings.Single(s => s.Setting == PanelSetting.BlueGain).Lock);
        Assert.Empty(locked.ExternallyChanged);

        // Filtre kapanınca kullanıcının 230'u geri gelir (filtre 255'e ezmez), panel yeniden yazılabilir.
        filter.Disable();
        Assert.Equal(230, rig.Monitor.Read(FakeHardware.Display, Blue)!.Value.Current);
        var unlocked = panel.Refresh();
        Assert.Empty(unlocked.ExternallyChanged);
        Assert.Equal(SettingLock.None, unlocked.Settings.Single(s => s.Setting == PanelSetting.BlueGain).Lock);
        Assert.Equal(PanelSetResult.Written, panel.Set(PanelSetting.BlueGain, 210));
    }

    private sealed class NoConflicts : IColorConflictProbe
    {
        public IReadOnlyList<ColorConflict> Detect() => [];
    }

    private sealed class NoOpPipeline : IColorPipeline
    {
        public void SetLayer(ColorLayer layer, MonitorTuner.Core.GammaRamp? table) { }
        public bool Apply() => true;
        public bool RestoreIfChangedExternally() => throw new NotSupportedException();
        public bool EnsureFullGammaRange() => throw new NotSupportedException();
        public void Shutdown() => throw new NotSupportedException();
        public bool ResetTable() => throw new NotSupportedException();
    }

    // ---- Giriş ----

    [Fact]
    public void Input_StaysOnConnectedPort_WithoutWarning()
    {
        var rig = new Rig();

        Assert.Equal(PanelSetResult.Unchanged, rig.Panel.Set(PanelSetting.Input, 17));
        Assert.Equal(["Vga", "Hdmi"], rig.State(PanelSetting.Input).Choices.Select(c => c.Name));
    }

    [Fact]
    public void Input_SwitchingAwayFromConnectedPort_NeedsExplicitWarningAcceptance()
    {
        var rig = new Rig(); // kablo HDMI'da, 17

        Assert.Equal(PanelSetResult.NeedsInputWarning, rig.Panel.Set(PanelSetting.Input, 1));
        Assert.Equal(0, rig.Monitor.WriteCount(InputCode));

        Assert.Equal(PanelSetResult.Written, rig.Panel.Set(PanelSetting.Input, 1, inputWarningAccepted: true));
        Assert.Equal(1, rig.Monitor.Read(FakeHardware.Display, InputCode)!.Value.Current);
    }

    [Fact]
    public void Input_UnknownValue_IsRejected()
    {
        var rig = new Rig();

        Assert.Equal(PanelSetResult.OutOfRange, rig.Panel.Set(PanelSetting.Input, 3, inputWarningAccepted: true));
        Assert.Empty(rig.Monitor.Writes);
    }

    // ---- Yazılamayan kod, yazma sonuçları ----

    [Fact]
    public void Set_CodeNotWritable_IsLocked_AndNeverSent()
    {
        var rig = new Rig();
        rig.Ddc.Writable.Remove(Volume);

        var volume = rig.State(PanelSetting.Volume);

        Assert.Equal(SettingLock.NotWritable, volume.Lock);
        Assert.Equal(PanelSetResult.Locked, rig.Panel.Set(PanelSetting.Volume, 10));
        Assert.Empty(rig.Monitor.Writes);
    }

    [Theory]
    [InlineData(DdcWriteResult.Throttled, PanelSetResult.Throttled)]
    [InlineData(DdcWriteResult.Failed, PanelSetResult.Failed)]
    [InlineData(DdcWriteResult.Forbidden, PanelSetResult.NotSupported)]
    [InlineData(DdcWriteResult.NoDdc, PanelSetResult.NoDdc)]
    public void Set_DdcRefuses_ResultIsReported_AndValueIsNotAssumed(DdcWriteResult refused, PanelSetResult expected)
    {
        var rig = new Rig();
        rig.Panel.Refresh();
        rig.Ddc.Override = _ => refused;

        Assert.Equal(expected, rig.Panel.Set(PanelSetting.Brightness, 40));

        // Monitörde değişen bir şey yok; sonraki okuma "dış değişiklik" saymaz.
        Assert.Equal(55, rig.State(PanelSetting.Brightness).Current);
        Assert.Empty(rig.Panel.Refresh().ExternallyChanged);
    }

    // ---- Yazma izin listesi ----

    [Fact]
    public void EveryPanelCode_IsWritableInProfile_AndNeverForbidden()
    {
        foreach (var setting in Enum.GetValues<PanelSetting>())
        {
            var code = DdcPanel.CodeOf(setting);
            Assert.False(SafetyLimits.IsDdcWriteForbidden(code), $"{setting}: {code:X2} yasak");
            Assert.Contains(code, Profile.WritableDdcCodes);
        }
    }

    [Fact]
    public void WholeSession_TouchesOnlyPanelCodes()
    {
        var rig = new Rig();
        rig.UseCustomRgb();
        foreach (var setting in Enum.GetValues<PanelSetting>())
            foreach (var value in new[] { 0, 1, 5, 17, 40, 100, 255 })
                rig.Panel.Set(setting, value, inputWarningAccepted: true);

        var allowed = Enum.GetValues<PanelSetting>().Select(DdcPanel.CodeOf).ToHashSet();
        Assert.All(rig.Monitor.Writes, w => Assert.Contains(w.Code, allowed));
        Assert.DoesNotContain(rig.Monitor.Writes, w => SafetyLimits.IsDdcWriteForbidden(w.Code));
        Assert.DoesNotContain(rig.Monitor.Writes, w => w.Code == 0xAE); // Hz'e dokunmaz
    }
}
