using Microsoft.Extensions.Time.Testing;
using MonitorTuner.Services;

namespace MonitorTuner.Tests.Services;

/// <summary>
/// T314 düzeltme: açılış ve beklenmeyen hatalar günlüğe yazılır (uygulama sessizce kapanınca sebebi görülsün). Günlük
/// uygulamayı hiçbir zaman düşürmez, dosya büyümez.
/// </summary>
public sealed class AppLogTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "mt-log-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 5, 9, 30, 15, TimeSpan.Zero));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void Write_AppendsTimestampedLines_AndCreatesTheFolder()
    {
        var log = new AppLog(_folder, _time);

        log.Write("açılış");
        _time.Advance(TimeSpan.FromSeconds(2));
        log.Write("tepsi hazır");

        var lines = File.ReadAllLines(log.FilePath);
        Assert.Equal(["2026-11-05 09:30:15  açılış", "2026-11-05 09:30:17  tepsi hazır"], lines);
        Assert.Equal(Path.Combine(_folder, AppLog.FileName), log.FilePath);
    }

    [Fact]
    public void Error_WritesWhereTypeMessageAndStack()
    {
        var log = new AppLog(_folder, _time);
        Exception error;
        try { throw new InvalidOperationException("dış", new IOException("iç neden")); }
        catch (Exception ex) { error = ex; }

        log.Error("pencere", error);

        var text = File.ReadAllText(log.FilePath);
        Assert.Contains("HATA (pencere)", text);
        Assert.Contains("InvalidOperationException: dış", text);
        Assert.Contains("iç neden", text); // iç hata da
        Assert.Contains(nameof(Error_WritesWhereTypeMessageAndStack), text); // yığın
    }

    [Fact]
    public void UnwritableFolder_NeverThrows()
    {
        Directory.CreateDirectory(_folder);
        var blocker = Path.Combine(_folder, "dosya");
        File.WriteAllText(blocker, "klasör değil");
        var log = new AppLog(blocker, _time); // klasör yerine dosya: yazılamaz

        log.Write("x");
        log.Error("y", new Exception("z"));
    }

    [Fact]
    public void LargeLog_IsRotated_SoItNeverGrowsWithoutBound()
    {
        var log = new AppLog(_folder, _time);
        Directory.CreateDirectory(_folder);
        File.WriteAllText(log.FilePath, new string('x', AppLog.MaxBytes + 1));

        log.Write("yeni");

        Assert.Equal(["2026-11-05 09:30:15  yeni"], File.ReadAllLines(log.FilePath));
        Assert.True(File.Exists(log.FilePath + ".old"));
    }

    [Fact]
    public void DefaultFolder_IsUnderTheSettingsRoot() =>
        Assert.Equal(Path.Combine("kök", "logs"), AppLog.DefaultFolder("kök"));
}
