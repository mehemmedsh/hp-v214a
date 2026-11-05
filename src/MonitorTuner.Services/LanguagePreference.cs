namespace MonitorTuner.Services;

/// <summary>
/// Arayüz dili (T502): Türkçe (ana dil, varsayılan), Azerbaycanca, İngilizce. Windows'un dili ne olursa olsun seçilmedikçe
/// Türkçe. Seçim ayar kökünde tek satırlık bir dosyada durur (monitörden bağımsız; desteklenen monitör bulunmasa da okunur).
/// Elle bozulmuş ya da okunamayan dosya Türkçe sayılır. Donanıma dokunmaz.
/// </summary>
public static class LanguagePreference
{
    public const string Default = "tr";
    public const string FileName = "language.txt";

    public static IReadOnlyList<string> Supported { get; } = ["tr", "az", "en"];

    public static string Load(string rootFolder)
    {
        try
        {
            var path = Path.Combine(rootFolder, FileName);
            if (!File.Exists(path)) return Default;
            var code = File.ReadAllText(path).Trim().ToLowerInvariant();
            return Supported.Contains(code) ? code : Default;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Default;
        }
    }

    /// <summary>Desteklenmeyen dil <see cref="ArgumentException"/>, hiçbir şey yazılmaz. Yeni dil uygulama yeniden açılınca görünür.</summary>
    public static void Save(string rootFolder, string code)
    {
        if (!Supported.Contains(code)) throw new ArgumentException($"Desteklenmeyen dil: '{code}'", nameof(code));
        Directory.CreateDirectory(rootFolder);
        File.WriteAllText(Path.Combine(rootFolder, FileName), code);
    }
}
