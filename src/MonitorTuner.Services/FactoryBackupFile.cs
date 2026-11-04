using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;

namespace MonitorTuner.Services;

/// <summary>
/// İlk açılış yedeğinin kalıcı yeri (<see cref="IFactoryBackupStore"/>): ayar deposunun yedek klasöründe (T102) tek JSON
/// dosyası. Uygulama ve kurtarma dosyası (T104) aynı dosyayı kullanır. Yoksa ya da bozuksa <see cref="Load"/> null döner;
/// bozuk dosya üstüne yazılmadan önce yanına kopyalanır. Yazma geçici dosyaya yapılıp yerine taşınır.
/// </summary>
public sealed class FactoryBackupFile(string backupFolder) : IFactoryBackupStore
{
    public const string FileName = "factory-backup.json";
    public const string CorruptCopyName = "factory-backup.bozuk.json";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    private sealed record Stored(Dictionary<byte, ushort>? Ddc, ushort[][]? GpuTable, NvidiaState? Nvidia, int? GammaRange);

    public string FilePath { get; } = Path.Combine(backupFolder, FileName);

    public FactoryBackup? Load() => File.Exists(FilePath) ? Parse(out _) : null;

    public void Save(FactoryBackup backup)
    {
        ArgumentNullException.ThrowIfNull(backup);
        if (File.Exists(FilePath) && Parse(out var corrupt) is null && corrupt)
            File.Copy(FilePath, Path.Combine(Path.GetDirectoryName(FilePath)!, CorruptCopyName), overwrite: true);

        var table = backup.GpuTable is { } t ? new[] { t.Red.ToArray(), t.Green.ToArray(), t.Blue.ToArray() } : null;
        var json = JsonSerializer.Serialize(new Stored(new(backup.Ddc), table, backup.Nvidia, backup.GammaRange), Options);
        var temp = FilePath + ".tmp";
        try
        {
            File.WriteAllText(temp, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temp, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private FactoryBackup? Parse(out bool corrupt)
    {
        corrupt = true;
        try
        {
            if (JsonSerializer.Deserialize<Stored>(File.ReadAllText(FilePath), Options) is not { Ddc: { } ddc } stored) return null;
            var table = stored.GpuTable switch
            {
                null => null,
                [var r, var g, var b] => new GammaRamp(r, g, b), // uzunluk yanlışsa ArgumentException
                _ => throw new JsonException("GPU tablosu üç kanal olmalı."),
            };
            corrupt = false;
            return new FactoryBackup(ddc, table, stored.Nvidia, stored.GammaRange);
        }
        catch (Exception e) when (e is JsonException or ArgumentException or NotSupportedException)
        {
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            corrupt = false; // okunamadı (kilitli); bozuk sayılıp kopyalanmaz
            return null;
        }
    }
}
