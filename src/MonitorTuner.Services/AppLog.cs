using System.Globalization;

namespace MonitorTuner.Services;

/// <summary>
/// Uygulama günlüğü (T314 düzeltme): açılış adımları ve beklenmeyen hatalar `%AppData%\MonitorTuner\logs\gunluk.txt`'ye yazılır.
/// Uygulama sessizce kapanırsa sebep buradan okunur; dosya hiç oluşmadıysa süreç hiç başlamamıştır (Windows engellemiştir).
/// Günlük hiçbir zaman istisna atmaz (yazılamazsa sessizce geçer) ve <see cref="MaxBytes"/>'ı aşınca eskisi `.old` olur.
/// Donanıma ve ayarlara dokunmaz; sadece metin.
/// </summary>
public sealed class AppLog(string folder, TimeProvider time)
{
    public const string FileName = "gunluk.txt";
    public const int MaxBytes = 256 * 1024;

    private readonly object _gate = new();

    public string FilePath { get; } = Path.Combine(folder, FileName);

    public static string DefaultFolder(string settingsRoot) => Path.Combine(settingsRoot, "logs");

    public void Write(string message)
    {
        var line = time.GetLocalNow().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "  " + message + Environment.NewLine;
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(folder);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes) File.Move(FilePath, FilePath + ".old", overwrite: true);
                File.AppendAllText(FilePath, line);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                // Günlük uygulamayı düşürmez.
            }
        }
    }

    /// <summary>Hatanın yeri, türü, iletisi, iç hataları ve yığını (<see cref="Exception.ToString"/>).</summary>
    public void Error(string where, Exception error) => Write($"HATA ({where}): {error}");
}
