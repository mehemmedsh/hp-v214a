using System.Collections.Concurrent;
using System.Globalization;
using System.Resources;

namespace MonitorTuner.App.Localization;

/// <summary>
/// Arayüz metinlerinin tek kaynağı. Anahtar "Özellik.Ad" biçimindedir; Özellik, Resources/Strings/Özellik.resx
/// dosyasıdır (özellik başına ayrı dosya, paralel görevler çakışmaz). Nötr dil Türkçe; Azerbaycanca ve
/// İngilizce T502'de Özellik.az.resx / Özellik.en.resx olarak eklenir, eksik çeviri Türkçeye düşer.
/// </summary>
public static class Loc
{
    private const string ResourceRoot = "MonitorTuner.App.Resources.Strings.";
    private static readonly ConcurrentDictionary<string, ResourceManager> Managers = new(StringComparer.Ordinal);

    public static string Get(string key, CultureInfo? culture = null)
    {
        var (feature, name) = Split(key);
        var manager = Managers.GetOrAdd(feature, f => new ResourceManager(ResourceRoot + f, typeof(Loc).Assembly));
        try
        {
            return manager.GetString(name, culture ?? CultureInfo.CurrentUICulture) ?? Missing(key);
        }
        catch (MissingManifestResourceException)
        {
            return Missing(key);
        }
    }

    private static (string Feature, string Name) Split(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var dot = key.IndexOf('.');
        if (dot <= 0 || dot == key.Length - 1 || key.IndexOf('.', dot + 1) >= 0)
            throw new ArgumentException($"Anahtar 'Özellik.Ad' biçiminde olmalı: '{key}'", nameof(key));
        return (key[..dot], key[(dot + 1)..]);
    }

    // Eksik metin uygulamayı çökertmez, ekranda işaretli görünür; testler eksik anahtarı derlemeden önce yakalar.
    private static string Missing(string key) => $"[[{key}]]";
}
