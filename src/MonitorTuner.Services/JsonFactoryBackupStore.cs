using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;

namespace MonitorTuner.Services;

/// <summary>
/// İlk açılış yedeğinin dosyası (T104): ayar deposunun yedek klasöründe tek JSON (uygulamanın tek monitörü). Uygulama
/// Güvenlik üzerinden yazar, kurtarma dosyası sadece okur. Yazma geçici dosyaya yapılıp yerine taşınır; bozuk dosya yedek
/// sayılmaz ve üzerine yazılmadan önce kenara kopyalanır. Okurken klasör oluşturmaz.
/// </summary>
public sealed class JsonFactoryBackupStore(string backupFolder) : IFactoryBackupStore
{
    public const string FileName = "ilk-acilis-yedegi.json";
    public const string CorruptCopyName = "ilk-acilis-yedegi.bozuk.json";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    private readonly string _path = Path.Combine(backupFolder ?? throw new ArgumentNullException(nameof(backupFolder)), FileName);
    private readonly Lock _gate = new();

    /// <summary><c>%AppData%\MonitorTuner\backups</c> (<see cref="JsonSettingsStore.BackupFolder"/>).</summary>
    public static string DefaultFolder() => Path.Combine(JsonSettingsStore.DefaultRoot(), "backups");

    /// <param name="GpuTable">Kırmızı, yeşil, mavi; null: okunamadı.</param>
    private sealed record Dto(Dictionary<byte, ushort>? Ddc, ushort[][]? GpuTable, NvidiaState? Nvidia, int? GammaRange);

    public FactoryBackup? Load()
    {
        lock (_gate) return File.Exists(_path) ? Parse(File.ReadAllText(_path)) : null;
    }

    public void Save(FactoryBackup backup)
    {
        ArgumentNullException.ThrowIfNull(backup);
        var table = backup.GpuTable is { } t ? new[] { t.Red.ToArray(), t.Green.ToArray(), t.Blue.ToArray() } : null;
        var json = JsonSerializer.Serialize(new Dto(new(backup.Ddc), table, backup.Nvidia, backup.GammaRange), Options);

        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            if (File.Exists(_path) && Parse(File.ReadAllText(_path)) is null)
                File.Copy(_path, Path.Combine(Path.GetDirectoryName(_path)!, CorruptCopyName), overwrite: true);
            var temp = _path + ".tmp";
            try
            {
                File.WriteAllText(temp, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                File.Move(temp, _path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }

    /// <summary>Biçimi bozuksa null.</summary>
    private static FactoryBackup? Parse(string json)
    {
        try
        {
            if (JsonSerializer.Deserialize<Dto>(json, Options) is not { Ddc: { } ddc } dto) return null;
            if (dto.GpuTable is { } t && (t.Length != 3 || t.Any(c => c is null))) return null;
            var table = dto.GpuTable is { } c ? new GammaRamp(c[0], c[1], c[2]) : null;
            return new FactoryBackup(ddc, table, dto.Nvidia, dto.GammaRange);
        }
        catch (Exception e) when (e is JsonException or ArgumentException or NotSupportedException)
        {
            return null; // GammaRamp kanal boyu yanlışsa ArgumentException
        }
    }
}
