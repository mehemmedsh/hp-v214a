using MonitorTuner.Core;

namespace MonitorTuner.Services;

/// <summary>Monitör tanıma (T102): bağlı ekranlardan model, seri no, bağlantı tipi ve kartı çıkarır.</summary>
public interface IMonitorIdentifier
{
    IReadOnlyList<MonitorInfo> Identify();
}

/// <summary>
/// Ayar deposu (T102): <c>%AppData%</c> altında JSON; yedekler ayrı klasörde. Sadece kendi dosyalarına yazar.
/// Her özellik kendi bölümünü (ör. "RefreshRates", "ColorProfiles") okur ve yazar; paralel görevler çakışmaz.
/// </summary>
public interface ISettingsStore
{
    T? Read<T>(MonitorKey monitor, string section);

    void Write<T>(MonitorKey monitor, string section, T value);

    string BackupFolder { get; }
}
