using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;
using MonitorTuner.Services;

namespace MonitorTuner.Tests.Services;

/// <summary>İlk açılış yedeğinin dosyası (T104): uygulama ve kurtarma dosyası aynı dosyayı okur.</summary>
public sealed class FactoryBackupFileTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "mt-factory-" + Guid.NewGuid().ToString("N"));

    public FactoryBackupFileTests() => Directory.CreateDirectory(folder);

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void SaveThenLoad_RoundTripsEveryField()
    {
        var backup = new FactoryBackup(
            new Dictionary<byte, ushort> { [0x10] = 80, [0x16] = 255 },
            GammaRamps.Scale(new RgbGains(1, 0.9, 0.8)), new NvidiaState(DitheringMode.Off, 60), 128);

        new FactoryBackupFile(folder).Save(backup);
        var loaded = new FactoryBackupFile(folder).Load(); // yeni açılış

        Assert.NotNull(loaded);
        Assert.Equal(backup.Ddc, loaded.Ddc);
        Assert.Equal(backup.GpuTable, loaded.GpuTable);
        Assert.Equal(backup.Nvidia, loaded.Nvidia);
        Assert.Equal(128, loaded.GammaRange);
    }

    /// <summary>null alanlar (okunamadı; GdiIcmGammaRange'de "değer yoktu") null kalır, 0 olmaz.</summary>
    [Fact]
    public void SaveThenLoad_KeepsNulls()
    {
        new FactoryBackupFile(folder).Save(new FactoryBackup(new Dictionary<byte, ushort>(), null, null, null));

        var loaded = new FactoryBackupFile(folder).Load();

        Assert.NotNull(loaded);
        Assert.Empty(loaded.Ddc);
        Assert.Null(loaded.GpuTable);
        Assert.Null(loaded.Nvidia);
        Assert.Null(loaded.GammaRange);
    }

    [Fact]
    public void Load_MissingFile_IsNull() => Assert.Null(new FactoryBackupFile(folder).Load());

    /// <summary>Bozuk dosya yok sayılır; üstüne yazılmadan önce yanına kopyalanır (asıl değerler elle kurtarılabilsin).</summary>
    [Fact]
    public void CorruptFile_LoadsAsNull_AndIsKeptBeforeOverwrite()
    {
        var file = new FactoryBackupFile(folder);
        File.WriteAllText(file.FilePath, "{ \"Ddc\": { \"16\": ");

        Assert.Null(file.Load());
        file.Save(new FactoryBackup(new Dictionary<byte, ushort>(), null, null, 256));

        Assert.Equal(256, file.Load()!.GammaRange);
        Assert.Equal("{ \"Ddc\": { \"16\": ", File.ReadAllText(Path.Combine(folder, FactoryBackupFile.CorruptCopyName)));
    }

    /// <summary>Kanal sayısı ya da uzunluğu yanlış tablo bozuk sayılır.</summary>
    [Fact]
    public void Load_WrongTableShape_IsNull()
    {
        var file = new FactoryBackupFile(folder);
        File.WriteAllText(file.FilePath, "{ \"Ddc\": {}, \"GpuTable\": [[1, 2], [3], [4]] }");

        Assert.Null(file.Load());
    }

    /// <summary>Güvenlik katmanı yedeği bu dosyaya yazar; ikinci açılış yedeği yeniden almaz.</summary>
    [Fact]
    public void SafetyLayer_WritesFactoryBackupHere_OncePerInstall()
    {
        var hw = new Fakes.FakeHardware();
        var store = new FactoryBackupFile(folder);
        Assert.True(hw.GammaRange.Write(128, Fakes.FakeRestorePoints.For(WriteTarget.GdiIcmGammaRange, Fakes.FakeHardware.Display)));
        Open(hw, store).CreateRestorePoint(new WriteRequest(WriteTarget.GdiIcmGammaRange, Fakes.FakeHardware.Display));
        Assert.True(hw.GammaRange.Write(256, Fakes.FakeRestorePoints.For(WriteTarget.GdiIcmGammaRange, Fakes.FakeHardware.Display)));

        Open(hw, new FactoryBackupFile(folder)).CreateRestorePoint(new WriteRequest(WriteTarget.GdiIcmGammaRange, Fakes.FakeHardware.Display));

        Assert.Equal(128, new FactoryBackupFile(folder).Load()!.GammaRange);
    }

    private static SafetyLayer Open(Fakes.FakeHardware hw, IFactoryBackupStore store) => new(
        Fakes.FakeHardware.Display, [], new SafetyHardware(hw.Monitor, hw.Gpu, hw.Modes, hw.Nvidia, hw.GammaRange),
        () => throw new NotSupportedException(), store, hw.Clock);
}
