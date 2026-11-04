using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;
using MonitorTuner.Services;

namespace MonitorTuner.Tests.Contracts;

/// <summary>İlk açılış yedeğinin dosyası (T104): ayar deposunun yedek klasöründe; kurtarma dosyası buradan okur.</summary>
public sealed class FactoryBackupStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "mt-factory-" + Guid.NewGuid().ToString("N"), "backups");

    public void Dispose()
    {
        var sandbox = Path.GetDirectoryName(_folder)!;
        if (Directory.Exists(sandbox)) Directory.Delete(sandbox, recursive: true);
    }

    /// <summary>Dosya yoksa yedek yok; okuma klasör oluşturmaz.</summary>
    [Fact]
    public void Load_WhenMissing_ReturnsNull_AndCreatesNothing()
    {
        Assert.Null(new JsonFactoryBackupStore(_folder).Load());
        Assert.False(Directory.Exists(_folder));
    }

    /// <summary>DDC değerleri, tablo, NVIDIA durumu ve GdiIcmGammaRange (null: değer yoktu) aynen geri okunur.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(256)]
    public void SaveThenLoad_RoundTrips(int? gammaRange)
    {
        var table = GammaRamps.Scale(new RgbGains(1, 0.9, 0.8));
        var backup = new FactoryBackup(
            new Dictionary<byte, ushort> { [0x10] = 90, [0x16] = 50 }, table, new NvidiaState(DitheringMode.Off, 55), gammaRange);

        new JsonFactoryBackupStore(_folder).Save(backup);
        var loaded = new JsonFactoryBackupStore(_folder).Load();

        Assert.NotNull(loaded);
        Assert.Equal(backup.Ddc, loaded.Ddc);
        Assert.Equal(table, loaded.GpuTable);
        Assert.Equal(backup.Nvidia, loaded.Nvidia);
        Assert.Equal(gammaRange, loaded.GammaRange);
        Assert.Equal([JsonFactoryBackupStore.FileName], Directory.GetFiles(_folder).Select(Path.GetFileName));
    }

    /// <summary>Okunamayan hedefler (null) null kalır.</summary>
    [Fact]
    public void SaveThenLoad_KeepsUnreadableTargetsNull()
    {
        new JsonFactoryBackupStore(_folder).Save(new FactoryBackup(new Dictionary<byte, ushort>(), null, null, null));

        var loaded = new JsonFactoryBackupStore(_folder).Load();

        Assert.NotNull(loaded);
        Assert.Empty(loaded.Ddc);
        Assert.Null(loaded.GpuTable);
        Assert.Null(loaded.Nvidia);
    }

    /// <summary>Bozuk dosya yedek sayılmaz (hata atılmaz); üzerine yazılmadan önce kenara kopyalanır.</summary>
    [Theory]
    [InlineData("{ bozuk")]
    [InlineData("""{ "Ddc": {}, "GpuTable": [[1, 2]], "Nvidia": null, "GammaRange": null }""")]
    public void Load_CorruptFile_ReturnsNull_AndSaveKeepsCopy(string content)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, JsonFactoryBackupStore.FileName), content);
        var store = new JsonFactoryBackupStore(_folder);

        Assert.Null(store.Load());

        store.Save(new FactoryBackup(new Dictionary<byte, ushort>(), GammaRamp.Identity, null, null));
        Assert.Equal(GammaRamp.Identity, store.Load()?.GpuTable);
        Assert.Equal(content, File.ReadAllText(Path.Combine(_folder, JsonFactoryBackupStore.CorruptCopyName)));
    }
}
