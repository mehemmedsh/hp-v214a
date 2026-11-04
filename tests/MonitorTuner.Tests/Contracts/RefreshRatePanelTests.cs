using MonitorTuner.Core;
using MonitorTuner.Core.Profiles;
using MonitorTuner.Safety;
using MonitorTuner.Services;
using MonitorTuner.Tests.Fakes;

namespace MonitorTuner.Tests.Contracts;

/// <summary>Hz paneli sözleşmesi (02-MIMARI.md, 03-GUVENLIK.md → 2): sahte sistem, sahte monitör ve sahte saatle (T209).</summary>
public class RefreshRatePanelTests
{
    private static readonly MonitorProfile Profile =
        new MonitorProfileReader().Read(File.ReadAllText(Path.Combine(RepoRoot.Find(), "profiles", "hp-p21b-g4.json")));

    private static readonly MonitorKey Key = new("HP P21b G4", "SN1", "HDMI", "RTX 3080");

    /// <summary>Onay gelmezse 15 sn sonra önceki moda dönülür; Windows'a kaydedilmez.</summary>
    [Fact]
    public void SwitchTo_WithoutConfirm_ReturnsToPreviousModeAfter15Seconds()
    {
        var rig = new Rig();
        Assert.Equal(RefreshSwitchResult.AwaitingConfirmation, rig.Panel.SwitchTo(76, outOfSpecWarningAccepted: true));
        Assert.True(rig.Panel.IsAwaitingConfirmation);
        Assert.Equal(76, rig.Panel.CurrentRefreshHz);

        rig.Hw.Clock.Advance(SafetyLimits.ConfirmationTimeout - TimeSpan.FromMilliseconds(1));
        Assert.Equal(76, rig.Hw.Modes.Current.RefreshHz);
        Assert.Empty(rig.Reverted);

        rig.Hw.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(60, rig.Hw.Modes.Current.RefreshHz);
        Assert.Equal([true], rig.Reverted);
        Assert.False(rig.Panel.IsAwaitingConfirmation);

        Assert.Equal(RefreshConfirmResult.TooLate, rig.Panel.Confirm());
        Assert.Equal(60, rig.Hw.Modes.Persisted.RefreshHz);
        Assert.Equal(0, rig.Hw.Modes.PersistCount);
    }

    /// <summary>Onaylanan mod kalır ve kaydedilir; onaysız geçişte uygulama çökerse yeniden başlatmada kayıtlı mod gelir.</summary>
    [Fact]
    public void Confirm_WithinTimeout_KeepsAndPersists_AndCrashFallsBackToPersisted()
    {
        var rig = new Rig();
        Assert.Equal(RefreshSwitchResult.AwaitingConfirmation, rig.Panel.SwitchTo(72)); // spec içi: uyarı yok
        rig.Hw.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(RefreshConfirmResult.Kept, rig.Panel.Confirm());
        rig.Hw.Clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(72, rig.Hw.Modes.Current.RefreshHz);
        Assert.Equal(72, rig.Hw.Modes.Persisted.RefreshHz);
        Assert.Empty(rig.Reverted);
        Assert.False(rig.Panel.IsAwaitingConfirmation);
        Assert.Equal(RefreshConfirmResult.TooLate, rig.Panel.Confirm()); // bekleyen yok

        rig.Panel.SwitchTo(76, outOfSpecWarningAccepted: true);
        rig.Hw.Modes.SimulateRestart(); // 15 sn içinde çöktü, onay ve geri dönüş olmadı
        Assert.Equal(72, rig.Hw.Modes.Current.RefreshHz);
    }

    [Fact]
    public void SwitchTo_OnlyApprovedRates_OneAtATime_AndSameRateWritesNothing()
    {
        var rig = new Rig();
        Assert.Equal([76, 75, 72, 60, 50], rig.Panel.ApprovedRatesHz);
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.Panel.SwitchTo(74)); // profilde var ama onaylı değil
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.Panel.SwitchTo(77));

        Assert.Equal(RefreshSwitchResult.Unchanged, rig.Panel.SwitchTo(60));
        Assert.Empty(rig.Hw.Modes.Switches);

        Assert.Equal(RefreshSwitchResult.AwaitingConfirmation, rig.Panel.SwitchTo(50));
        Assert.Equal(RefreshSwitchResult.Busy, rig.Panel.SwitchTo(72));
        Assert.Equal([50], rig.Hw.Modes.Switches.Select(s => s.Mode.RefreshHz));
    }

    /// <summary>72 Hz üstüne ilk geçişte uyarı kabul edilmeden geçilmez; kabul mod başına hatırlanır.</summary>
    [Fact]
    public void SwitchTo_OutOfSpecFirstTime_NeedsWarning()
    {
        var rig = new Rig();
        Assert.Equal(RefreshSwitchResult.NeedsOutOfSpecWarning, rig.Panel.SwitchTo(75));
        Assert.Empty(rig.Hw.Modes.Switches);

        Assert.Equal(RefreshSwitchResult.AwaitingConfirmation, rig.Panel.SwitchTo(75, outOfSpecWarningAccepted: true));
        rig.Panel.Confirm();
        rig.Panel.SwitchTo(60);
        rig.Panel.Confirm();

        var again = rig.NewPanel(); // uygulama yeniden açıldı, aynı ayar deposu
        Assert.Equal(RefreshSwitchResult.AwaitingConfirmation, again.SwitchTo(75));
        again.Confirm();
        Assert.Equal(RefreshSwitchResult.NeedsOutOfSpecWarning, again.SwitchTo(76));
        Assert.Equal(75, rig.Hw.Modes.Current.RefreshHz);
    }

    /// <summary>Sürücünün sildiği onaylı modlar listelenir; hiç eklenmemiş mod uyarı vermez. NVIDIA değerleri profilden.</summary>
    [Fact]
    public void MissingRates_ListsApprovedModesDeletedByDriver()
    {
        var rig = new Rig(76, 75, 50); // 72 henüz eklenmedi
        Assert.Equal([76, 75, 60, 50], rig.Panel.GetAvailableRatesHz());
        Assert.Empty(rig.Panel.GetMissingRatesHz());
        Assert.Equal(RefreshSwitchResult.ModeMissing, rig.Panel.SwitchTo(72));

        rig.Hw.Modes.SimulateDriverDroppedMode(new(1920, 1080, 76));
        Assert.Equal([76], rig.Panel.GetMissingRatesHz());
        Assert.Equal([76], rig.NewPanel().GetMissingRatesHz()); // görülenler kalıcı
        Assert.Equal(RefreshSwitchResult.ModeMissing, rig.Panel.SwitchTo(76, outOfSpecWarningAccepted: true));
        Assert.Empty(rig.Hw.Modes.Switches);

        var values = rig.Panel.GetNvidiaValues(76)!;
        Assert.Equal((2080, 1092), (values.Timing.HTotal, values.Timing.VTotal));
        Assert.Equal(ModeSource.CustomResolution, values.Source);
        Assert.Null(rig.Panel.GetNvidiaValues(50)); // EDID'de hazır
        Assert.Null(rig.Panel.GetNvidiaValues(60));
    }

    /// <summary>Masaüstü başka çözünürlükteyken (ör. oyun 1440p'ye aldı) 1080p'de görülmüş modlar "silinmiş" sayılmaz.</summary>
    [Fact]
    public void MissingRates_IgnoreCurrentResolution()
    {
        var rig = new Rig();
        Assert.Empty(rig.Panel.GetMissingRatesHz()); // 76, 75, 72, 60, 50 görüldü

        rig.Hw.Modes.SimulateExternalSwitch(new(2560, 1440, 60));
        Assert.Empty(rig.Panel.GetMissingRatesHz());
        Assert.Empty(rig.NewPanel().GetMissingRatesHz());

        rig.Hw.Modes.SimulateDriverDroppedMode(new(1920, 1080, 76)); // gerçek silme yine görülür
        Assert.Equal([76], rig.Panel.GetMissingRatesHz());
    }

    /// <summary>
    /// Silinme kararı profildeki çözünürlüğe (1080p) göre verilir: başka çözünürlükte aynı Hz'in olması 1080p@75'in
    /// silindiğini gizlemez; masaüstünün şu anki çözünürlüğü de sonucu değiştirmez.
    /// </summary>
    [Fact]
    public void MissingRates_UseProfileResolution_NotOtherResolutions()
    {
        var rig = new Rig();
        rig.Hw.Modes.SimulateExternalSwitch(new(1680, 1050, 75)); // 1680x1050@75 listeye girer
        rig.Hw.Modes.SimulateExternalSwitch(new(1920, 1080, 60));
        Assert.Empty(rig.Panel.GetMissingRatesHz());

        rig.Hw.Modes.SimulateDriverDroppedMode(new(1920, 1080, 75));
        Assert.Equal([75], rig.Panel.GetMissingRatesHz());

        rig.Hw.Modes.SimulateExternalSwitch(new(2560, 1440, 60));
        Assert.Equal([75], rig.Panel.GetMissingRatesHz());
        Assert.Equal([75], rig.NewPanel().GetMissingRatesHz());
    }

    /// <summary>"Görüldü" kaydı da sadece profildeki çözünürlükten yazılır: 1080p'de hiç eklenmemiş mod uyarı vermez.</summary>
    [Fact]
    public void MissingRates_SeenOnlyAtProfileResolution()
    {
        var rig = new Rig(76, 75, 50); // 1080p@72 yok
        rig.Hw.Modes.SimulateExternalSwitch(new(1680, 1050, 72));
        Assert.Empty(rig.Panel.GetMissingRatesHz());
        Assert.Equal([50, 60, 75, 76], rig.Store.Read<RefreshRateSettings>(Key, RefreshRatePanel.SettingsSection)!.SeenRatesHz);

        rig.Hw.Modes.SimulateDriverDroppedMode(new(1680, 1050, 72));
        Assert.Empty(rig.Panel.GetMissingRatesHz());
    }

    /// <summary>Liste ya da şu anki mod okunamıyorsa uyarı verilmez ve görülenler kaydı değişmez.</summary>
    [Fact]
    public void MissingRates_WhenListUnreadable_ReturnsEmptyAndKeepsSeen()
    {
        var rig = new Rig();
        Assert.Empty(rig.Panel.GetMissingRatesHz());
        var seen = rig.Store.Read<RefreshRateSettings>(Key, RefreshRatePanel.SettingsSection)!.SeenRatesHz;
        Assert.Equal([50, 60, 72, 75, 76], seen);

        rig.Hw.Modes.ListUnavailable = true;
        Assert.Empty(rig.Panel.GetMissingRatesHz());
        rig.Hw.Modes.ListUnavailable = false;
        rig.Hw.Modes.CurrentUnavailable = true;
        Assert.Empty(rig.Panel.GetMissingRatesHz());
        rig.Hw.Modes.CurrentUnavailable = false;

        Assert.Same(seen, rig.Store.Read<RefreshRateSettings>(Key, RefreshRatePanel.SettingsSection)!.SeenRatesHz); // yazılmadı
        rig.Hw.Modes.SimulateDriverDroppedMode(new(1920, 1080, 75));
        Assert.Equal([75], rig.Panel.GetMissingRatesHz()); // kayıt bozulmadı
    }

    /// <summary>Gerçek Hz monitörden (DDC AE) okunur; okumalar Hz'i değiştirmez.</summary>
    [Fact]
    public void ReadMonitorRefreshHz_UsesDdcAe()
    {
        var rig = new Rig();
        Assert.Equal(60, rig.Panel.ReadMonitorRefreshHz());
        Assert.True(rig.Panel.VerifyWithMonitor());

        rig.Panel.SwitchTo(76, outOfSpecWarningAccepted: true);
        Assert.Equal(76, rig.Panel.ReadMonitorRefreshHz());
        Assert.True(rig.Panel.VerifyWithMonitor());
        rig.Panel.Confirm();

        rig.Hw.Monitor.SetMonitorValue(0xAE, 7500); // monitör başka Hz görüyor
        Assert.Equal(75, rig.Panel.ReadMonitorRefreshHz());
        Assert.False(rig.Panel.VerifyWithMonitor());

        rig.Hw.Monitor.Responding = false;
        Assert.Null(rig.Panel.ReadMonitorRefreshHz());
        Assert.Null(rig.Panel.VerifyWithMonitor());

        rig.Panel.GetMissingRatesHz();
        rig.Hw.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal([76], rig.Hw.Modes.Switches.Select(s => s.Mode.RefreshHz)); // kendiliğinden geçiş yok
    }

    [Fact]
    public void WindowsRejects_SwitchFails_AndFailedRollBackIsReported()
    {
        var rig = new Rig();
        rig.Hw.Modes.RejectSwitches = true;
        Assert.Equal(RefreshSwitchResult.Failed, rig.Panel.SwitchTo(72));
        Assert.False(rig.Panel.IsAwaitingConfirmation);

        rig.Hw.Modes.RejectSwitches = false;
        rig.Panel.SwitchTo(72);
        rig.Hw.Modes.RejectSwitches = true; // geri dönüş de reddedilir
        rig.Hw.Clock.Advance(SafetyLimits.ConfirmationTimeout);

        Assert.Equal([false], rig.Reverted);
        Assert.False(rig.Panel.IsAwaitingConfirmation);
        Assert.Equal(60, rig.Hw.Modes.Persisted.RefreshHz); // yeniden başlatmada 60 gelir
    }

    [Fact]
    public void Confirm_WhenWindowsCannotSave_KeepsModeButReportsNotSaved()
    {
        var rig = new Rig();
        rig.Panel.SwitchTo(72);
        rig.Hw.Modes.RejectSwitches = true; // sahtede kaydetme de reddedilir
        Assert.Equal(RefreshConfirmResult.NotSaved, rig.Panel.Confirm());
        rig.Hw.Clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(72, rig.Hw.Modes.Current.RefreshHz);
        Assert.Equal(60, rig.Hw.Modes.Persisted.RefreshHz);
        Assert.Empty(rig.Reverted);
    }

    private sealed class Rig
    {
        public Rig(params int[] rates)
        {
            Hw = new FakeHardware(rates.Length > 0 ? rates : [76, 75, 72, 50]);
            Ddc = new DirectDdcControl(Hw.Monitor, Profile);
            Safety = new SafetyLayer(
                FakeHardware.Display, [], new SafetyHardware(Hw.Monitor, Hw.Gpu, Hw.Modes, Hw.Nvidia, Hw.GammaRange),
                () => throw new NotSupportedException("Hz paneli GPU tablosuna dokunmaz."), new MemoryBackupStore(), Hw.Clock);
            Panel = NewPanel();
        }

        public FakeHardware Hw { get; }
        public DirectDdcControl Ddc { get; }
        public SafetyLayer Safety { get; }
        public FakeStore Store { get; } = new();
        public RefreshRatePanel Panel { get; }
        public List<bool> Reverted { get; } = [];

        public RefreshRatePanel NewPanel()
        {
            var panel = new RefreshRatePanel(FakeHardware.Display, Profile, Hw.Modes, Ddc, Safety, Store, Key);
            panel.Reverted += Reverted.Add;
            return panel;
        }
    }

    private sealed class FakeStore : ISettingsStore
    {
        private readonly Dictionary<string, object> sections = [];
        public string BackupFolder => "";
        public T? Read<T>(MonitorKey monitor, string section) => sections.TryGetValue(section, out var v) && v is T t ? t : default;
        public void Write<T>(MonitorKey monitor, string section, T value) => sections[section] = value!;
    }

    private sealed class MemoryBackupStore : IFactoryBackupStore
    {
        private FactoryBackup? saved;
        public FactoryBackup? Load() => saved;
        public void Save(FactoryBackup backup) => saved = backup;
    }
}
