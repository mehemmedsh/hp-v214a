using Microsoft.Extensions.Time.Testing;
using MonitorTuner.Core;
using MonitorTuner.Services;

namespace MonitorTuner.Tests.Services;

/// <summary>
/// Mola hatırlatıcısı (T324, 08-GORUNTU-YONTEMLERI.md → 6): 20 dakika etkin kullanımda bir kez hatırlatır; 5 dakika boşta kalmak
/// moladır; film ve oyun modlarında araya girmez; varsayılan kapalı ve seçim kalıcı. Ekran ayarına dokunacak bağımlılığı yok.
/// </summary>
public class BreakReminderTests
{
    private static readonly MonitorKey Key = new("HP P21b G4", "SN1", "HDMI", "RTX 3080");
    private static readonly TimeSpan Active = TimeSpan.FromSeconds(2); // kullanıcı az önce fareyi oynattı

    private sealed class MemoryStore : ISettingsStore
    {
        public Dictionary<string, object> Sections { get; } = [];
        public int Writes { get; private set; }
        public string BackupFolder => "";
        public T? Read<T>(MonitorKey monitor, string section) => Sections.TryGetValue(section, out var v) && v is T t ? t : default;

        public void Write<T>(MonitorKey monitor, string section, T value)
        {
            Writes++;
            Sections[section] = value!;
        }
    }

    private static (BreakReminder Reminder, FakeTimeProvider Time, MemoryStore Store) Create(bool enabled = true)
    {
        var time = new FakeTimeProvider();
        var store = new MemoryStore();
        var reminder = new BreakReminder(store, Key, time) { Enabled = enabled };
        return (reminder, time, store);
    }

    /// <summary>Kullanıcı ekranda çalışırken her 5 sn'de bir zamanlayıcı; toplam süre boyunca kaç kez hatırlattı.</summary>
    private static int Run(BreakReminder reminder, FakeTimeProvider time, TimeSpan duration, TimeSpan idle, bool paused = false)
    {
        var count = 0;
        for (var t = TimeSpan.Zero; t < duration; t += TimeSpan.FromSeconds(5))
        {
            time.Advance(TimeSpan.FromSeconds(5));
            if (reminder.Tick(idle, paused)) count++;
        }
        return count;
    }

    [Fact]
    public void IsOffByDefault_AndNeverReminds()
    {
        var time = new FakeTimeProvider();
        var reminder = new BreakReminder(new MemoryStore(), Key, time);

        Assert.False(reminder.Enabled);
        Assert.Equal(0, Run(reminder, time, TimeSpan.FromHours(2), Active));
    }

    [Fact]
    public void Reminds_OnceEvery20MinutesOfActiveUse()
    {
        var (reminder, time, _) = Create();

        Assert.Equal(0, Run(reminder, time, TimeSpan.FromMinutes(19), Active));
        Assert.Equal(1, Run(reminder, time, TimeSpan.FromMinutes(2), Active));
        Assert.Equal(1, Run(reminder, time, TimeSpan.FromMinutes(20), Active));
        Assert.Equal(3, Run(reminder, time, TimeSpan.FromMinutes(60), Active));
    }

    /// <summary>5 dakika boşta kalmak (kalkmak, uzağa bakmak) doğal moladır: sayaç sıfırlanır.</summary>
    [Fact]
    public void FiveIdleMinutes_CountAsABreak()
    {
        var (reminder, time, _) = Create();
        Run(reminder, time, TimeSpan.FromMinutes(15), Active);

        Assert.Equal(0, Run(reminder, time, TimeSpan.FromMinutes(6), BreakReminder.IdleReset));
        Assert.Equal(0, Run(reminder, time, TimeSpan.FromMinutes(19), Active)); // moladan sonra yeni 20 dakika
        Assert.Equal(1, Run(reminder, time, TimeSpan.FromMinutes(2), Active));
    }

    /// <summary>Kısa duraklamalar (okurken fareye dokunmamak) mola sayılmaz.</summary>
    [Fact]
    public void ShortPauses_DoNotResetTheCount()
    {
        var (reminder, time, _) = Create();
        Run(reminder, time, TimeSpan.FromMinutes(10), Active);

        Run(reminder, time, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(3));

        Assert.Equal(1, Run(reminder, time, TimeSpan.FromMinutes(8), Active));
    }

    /// <summary>Film ya da oyun modu ekrandayken hatırlatma araya girmez; mod değişince yeni 20 dakika başlar.</summary>
    [Fact]
    public void WatchingOrPlaying_PausesTheReminder()
    {
        var (reminder, time, _) = Create();

        Assert.Equal(0, Run(reminder, time, TimeSpan.FromHours(2), Active, paused: true));
        Assert.Equal(0, Run(reminder, time, TimeSpan.FromMinutes(19), Active));
        Assert.Equal(1, Run(reminder, time, TimeSpan.FromMinutes(2), Active));
    }

    [Fact]
    public void Choice_IsSaved_AndSurvivesAReopen()
    {
        var (reminder, time, store) = Create(enabled: false);

        reminder.Enabled = true;
        reminder.Enabled = true; // aynı değer tekrar yazılmaz

        Assert.Equal(1, store.Writes);
        Assert.True(new BreakReminder(store, Key, time).Enabled);
    }

    /// <summary>Açınca sayaç baştan: hemen hatırlatmaz.</summary>
    [Fact]
    public void Enabling_StartsAFresh20Minutes()
    {
        var (reminder, time, _) = Create(enabled: false);
        time.Advance(TimeSpan.FromHours(1));

        reminder.Enabled = true;

        Assert.Equal(0, Run(reminder, time, TimeSpan.FromMinutes(19), Active));
    }

    [Theory]
    [InlineData("Sinema", true)]
    [InlineData("Film", true)]
    [InlineData("Sinema · Gece", true)]
    [InlineData("Oyun", true)]
    [InlineData("Konfor · Akşam", false)]
    [InlineData("DaVinci zaman çizelgesi", false)]
    [InlineData("Gündüz", false)]
    public void WatchingOrPlaying_IsCinemaOrGame(string mode, bool expected) =>
        Assert.Equal(expected, BreakReminder.IsWatchingOrPlaying(new ActiveMode(mode, Adjusted: true), ColorProfiles.BuiltIns));

    [Fact]
    public void NoActiveMode_IsNotWatching() =>
        Assert.False(BreakReminder.IsWatchingOrPlaying(null, ColorProfiles.BuiltIns));

    /// <summary>Ekran ayarına ulaşacak bağımlılık yok: sadece ayar deposu, monitör anahtarı ve saat.</summary>
    [Fact]
    public void HasNoDisplayDependencies() =>
        Assert.Equal(
            new[] { typeof(ISettingsStore), typeof(MonitorKey), typeof(TimeProvider) },
            typeof(BreakReminder).GetConstructors().Single().GetParameters().Select(p => p.ParameterType));
}
