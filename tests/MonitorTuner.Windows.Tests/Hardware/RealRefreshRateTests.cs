using System.IO;
using MonitorTuner.Core;
using MonitorTuner.Core.Profiles;
using MonitorTuner.Hardware;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;
using MonitorTuner.Services;
using MonitorTuner.Tests;
using Xunit.Abstractions;

namespace MonitorTuner.Windows.Tests.Hardware;

/// <summary>
/// 🔧 T209: gerçek monitörde 60 ↔ 76 geçişi ve geri dönüş. Varsayılan atlanır (CI ve ajanlar çalıştırmaz); sadece kullanıcı
/// ortam değişkeniyle açar. Yol uygulamanınkiyle aynı: RefreshRatePanel → Güvenlik (15 sn) → WindowsDisplayModes; AE DDC'den.
/// </summary>
public class RealRefreshRateTests(ITestOutputHelper output)
{
    private static readonly MonitorKey Key = new("HP P21b G4", "test", "HDMI", "RTX 3080");

    /// <summary>
    /// MONITORTUNER_REAL_HZ=1, yaklaşık 50 sn: 76 Hz'e geçer, onay vermez, 15 sn sonra 60'a döner; sonra 76'yı onaylar
    /// (kaydedilir), en sonda 60'a geçip onaylar. Test 60 Hz'de başlar ve biter.
    /// </summary>
    [RealMonitorFact("MONITORTUNER_REAL_HZ")]
    public void Real_60To76_RollsBackWithoutConfirm_ThenKeepsWithConfirm_AndEndsAt60()
    {
        var (panel, modes, safety, display) = Open();
        try
        {
            Assert.True(panel.CurrentRefreshHz == 60, $"Test 60 Hz'de başlamalı (şu an {panel.CurrentRefreshHz}).");
            Assert.True(panel.GetAvailableRatesHz().Contains(76), "76 Hz Windows listesinde yok: NVIDIA özel çözünürlüğünü ekle.");
            bool? returned = null;
            using var reverted = new ManualResetEventSlim();
            panel.Reverted += ok => { returned = ok; reverted.Set(); };

            // 1) Onay yok: Güvenlik 15 sn sonra 60'a döndürür, kayıtlı mod 60 kalır.
            Assert.Equal(RefreshSwitchResult.AwaitingConfirmation, panel.SwitchTo(76, outOfSpecWarningAccepted: true));
            Assert.Equal(76, WaitForMonitorHz(panel, 76));
            Assert.True(reverted.Wait(TimeSpan.FromSeconds(25)), "15 sn sonra geri dönüş olmadı.");
            Assert.True(returned, "Geri dönüş başarısız.");
            Assert.Equal(60, panel.CurrentRefreshHz);
            Assert.Equal(60, WindowsDisplayModes.ReadSavedMode(display)?.RefreshHz);
            Assert.Equal(60, WaitForMonitorHz(panel, 60));

            // 2) Onay var: 76 kalır ve Windows'a kaydedilir.
            Assert.Equal(RefreshSwitchResult.AwaitingConfirmation, panel.SwitchTo(76));
            Assert.Equal(76, WaitForMonitorHz(panel, 76));
            Assert.Equal(RefreshConfirmResult.Kept, panel.Confirm());
            Assert.Equal(76, WindowsDisplayModes.ReadSavedMode(display)?.RefreshHz);
            Thread.Sleep(SafetyLimits.ConfirmationTimeout + TimeSpan.FromSeconds(2));
            Assert.Equal(76, panel.CurrentRefreshHz);
            Assert.True(panel.VerifyWithMonitor(), "Monitörün gördüğü Hz Windows'unkiyle aynı değil.");

            // 3) 60'a geri, onaylı.
            Assert.Equal(RefreshSwitchResult.AwaitingConfirmation, panel.SwitchTo(60));
            Assert.Equal(60, WaitForMonitorHz(panel, 60));
            Assert.Equal(RefreshConfirmResult.Kept, panel.Confirm());
            Assert.Equal(60, WindowsDisplayModes.ReadSavedMode(display)?.RefreshHz);
        }
        finally
        {
            ReturnTo60(modes, safety, display); // test yarıda kalırsa ekran 76'da kalmasın
        }
    }

    /// <summary>Monitör geçişten sonra birkaç saniye senkron arar; AE'yi 10 sn'ye kadar bekler.</summary>
    private int? WaitForMonitorHz(RefreshRatePanel panel, int expected)
    {
        int? seen = null;
        for (var i = 0; i < 20 && seen != expected; i++)
        {
            Thread.Sleep(500);
            seen = panel.ReadMonitorRefreshHz();
        }
        output.WriteLine($"Windows {panel.CurrentRefreshHz} Hz · monitör (AE) {seen?.ToString() ?? "okunamadı"} Hz");
        return seen;
    }

    private static void ReturnTo60(WindowsDisplayModes modes, SafetyLayer safety, DisplayId display)
    {
        if (modes.GetCurrentMode(display) is not { } current || current.RefreshHz == 60) return;
        var target = current with { RefreshHz = 60 };
        if (modes.SwitchMode(display, target, safety.CreateRestorePoint(new WriteRequest(WriteTarget.DisplayMode, display))))
            modes.PersistMode(display, target, safety.CreateRestorePoint(new WriteRequest(WriteTarget.DisplayMode, display)));
    }

    private static (RefreshRatePanel, WindowsDisplayModes, SafetyLayer, DisplayId) Open()
    {
        var profile = new MonitorProfileReader().Read(File.ReadAllText(Path.Combine(RepoRoot.Find(), "profiles", "hp-p21b-g4.json")));
        var monitor = new WindowsDdcMonitor();
        var found = WindowsDdcMonitor.ListDisplays()
            .Where(d => monitor.ReadCapabilities(d)?.Contains("P21b", StringComparison.OrdinalIgnoreCase) == true).ToList();
        Assert.True(found.Count == 1, $"HP P21b G4 {found.Count} ekranda bulundu (1 olmalı): DDC/CI açık mı (OSD → Management)?");
        var display = found[0];

        var modes = new WindowsDisplayModes();
        var safety = new SafetyLayer(
            display, [], new SafetyHardware(monitor, new NoGpu(), modes, new NoNvidia(), new NoGammaRange()),
            () => throw new NotSupportedException(), new MemoryBackupStore(), TimeProvider.System);
        var ddc = new DdcControl(display, monitor, safety, profile, TimeProvider.System);
        return (new RefreshRatePanel(display, profile, modes, ddc, safety, new MemoryStore(), Key), modes, safety, display);
    }

    // GPU tablosu, NVIDIA ve kayıt defteri bu testte okunmaz ve yazılmaz; DDC sadece AE okunur.
    private sealed class NoGpu : IGpuGammaTable
    {
        public GammaRamp? Read(DisplayId display) => null;
        public bool Write(DisplayId display, GammaRamp table, RestorePoint restorePoint) => false;
    }

    private sealed class NoNvidia : INvidiaSettings
    {
        public NvidiaState? Read(DisplayId display) => null;
        public bool SetDithering(DisplayId display, DitheringMode mode, RestorePoint restorePoint) => false;
        public bool SetVibrance(DisplayId display, int vibrance, RestorePoint restorePoint) => false;
        public bool ResetToDefaults(DisplayId display, RestorePoint restorePoint) => false;
    }

    private sealed class NoGammaRange : IGdiIcmGammaRange
    {
        public int? Read() => null;
        public bool Write(int? value, RestorePoint restorePoint) => false;
    }

    private sealed class MemoryBackupStore : IFactoryBackupStore
    {
        private FactoryBackup? saved;
        public FactoryBackup? Load() => saved;
        public void Save(FactoryBackup backup) => saved = backup;
    }

    private sealed class MemoryStore : ISettingsStore
    {
        private readonly Dictionary<string, object> sections = [];
        public string BackupFolder => "";
        public T? Read<T>(MonitorKey monitor, string section) => sections.TryGetValue(section, out var v) && v is T t ? t : default;
        public void Write<T>(MonitorKey monitor, string section, T value) => sections[section] = value!;
    }
}
