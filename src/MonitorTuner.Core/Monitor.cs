namespace MonitorTuner.Core;

/// <summary>Windows'un ekran kimliği (ör. <c>\\.\DISPLAY1</c>); donanım arayüzleri hangi ekrana yazılacağını bununla alır.</summary>
public readonly record struct DisplayId(string Value);

public enum ConnectionType { Unknown, Hdmi, DisplayPort, Dvi, Vga }

/// <summary>Monitör tanımanın çıktısı (T102).</summary>
public sealed record MonitorInfo(
    DisplayId Display, string Model, string SerialNumber, ConnectionType Connection, string Port, string AdapterName)
{
    /// <summary>Kullanıcı ayarlarının anahtarı: monitör + seri no + port + kart (02-MIMARI.md → Profil yapısı).</summary>
    public MonitorKey Key => new(Model, SerialNumber, Port, AdapterName);
}

public readonly record struct MonitorKey(string Model, string SerialNumber, string Port, string AdapterName);
