using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MonitorTuner.Core;

namespace MonitorTuner.Services;

/// <summary>
/// Ayar deposu (T102): monitör başına bir JSON dosyası (<c>settings/</c>), her özellik kendi bölümünde.
/// Yedekler ayrı klasörde (<c>backups/</c>). Sadece kendi klasöründeki dosyalara yazar; dosya adı anahtardan
/// türetilir ama sadece harf, rakam ve tire içerir (yol kaçışı olmaz).
/// </summary>
/// <remarks>
/// Yazma geçici dosyaya yapılıp yerine taşınır: dosya her an eski ya da yeni tam hâlindedir. Kilit tek süreç içindir
/// (uygulamanın tek süreci var, <c>02-MIMARI.md</c>). Bozuk dosya okumada yok sayılır, ezilmeden önce yedek klasörüne kopyalanır.
/// </remarks>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    private readonly string _settingsFolder;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();

    /// <param name="rootFolder">Ayar ve yedek klasörlerinin üstü; uygulamada <see cref="DefaultRoot"/>.</param>
    public JsonSettingsStore(string rootFolder, TimeProvider time)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootFolder);
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _settingsFolder = Path.Combine(rootFolder, "settings");
        BackupFolder = Path.Combine(rootFolder, "backups");
        Directory.CreateDirectory(_settingsFolder);
        Directory.CreateDirectory(BackupFolder);
    }

    /// <summary>İlk açılış yedeği gibi yedeklerin klasörü; depo oluşturulurken var edilir.</summary>
    public string BackupFolder { get; }

    /// <summary><c>%AppData%\MonitorTuner</c>.</summary>
    public static string DefaultRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MonitorTuner");

    /// <summary>Bölüm yoksa, dosya bozuksa ya da bölüm istenen türde değilse <c>default</c>.</summary>
    public T? Read<T>(MonitorKey monitor, string section)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        lock (_gate)
        {
            var sections = Load(FileFor(monitor), out _)?["sections"]?.AsObject();
            if (sections is null || !sections.TryGetPropertyValue(section, out var node) || node is null) return default;
            try { return node.Deserialize<T>(Options); }
            catch (JsonException) { return default; }
        }
    }

    public void Write<T>(MonitorKey monitor, string section, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentNullException.ThrowIfNull(value);
        var node = JsonSerializer.SerializeToNode(value, Options);
        var path = FileFor(monitor);

        lock (_gate)
        {
            var root = Load(path, out var corrupt);
            if (corrupt) CopyCorruptToBackup(path);
            root ??= new JsonObject();
            root["monitor"] = JsonSerializer.SerializeToNode(monitor, Options);
            if (root["sections"] is not JsonObject sections) root["sections"] = sections = new JsonObject();
            sections[section] = node;

            var temp = path + ".tmp";
            try
            {
                File.WriteAllText(temp, root.ToJsonString(Options), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                File.Move(temp, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }

    /// <summary>Dosya yoksa null (<paramref name="corrupt"/> false); okunamıyor ya da biçimi bozuksa null (<paramref name="corrupt"/> true).</summary>
    private static JsonObject? Load(string path, out bool corrupt)
    {
        corrupt = false;
        if (!File.Exists(path)) return null;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is JsonObject root && root["sections"] is JsonObject or null) return root;
        }
        catch (JsonException) { }
        corrupt = true;
        return null;
    }

    private void CopyCorruptToBackup(string path)
    {
        var stamp = _time.GetUtcNow().ToString("yyyyMMdd-HHmmss");
        var name = $"{Path.GetFileNameWithoutExtension(path)}.bozuk-{stamp}.json";
        File.Copy(path, Path.Combine(BackupFolder, name), overwrite: true);
    }

    /// <summary>Okunur ad (model ve seri) + anahtarın özeti: iki farklı anahtar aynı dosyaya düşmez.</summary>
    private string FileFor(MonitorKey key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join('\u001f', key.Model, key.SerialNumber, key.Port, key.AdapterName))))[..16];
        return Path.Combine(_settingsFolder, $"{Slug(key.Model)}-{Slug(key.SerialNumber)}-{hash}.json");
    }

    private static string Slug(string text)
    {
        var slug = new string(text.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        return slug.Length > 24 ? slug[..24] : slug;
    }
}
