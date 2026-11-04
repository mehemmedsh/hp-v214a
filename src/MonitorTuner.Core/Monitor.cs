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

    /// <summary>VGA'da görüntü bu kartlarda analog; arayüz "HDMI'a geç" uyarısı verir.</summary>
    public bool IsVga => Connection == ConnectionType.Vga;

    /// <summary>Tanınan monitör bu profilin monitörü mü (büyük/küçük harf farkı sayılmaz). Model okunamadıysa hayır.</summary>
    public bool Matches(Profiles.MonitorProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Model.Length > 0 && string.Equals(Model, profile.Model, StringComparison.OrdinalIgnoreCase);
    }
}

public readonly record struct MonitorKey(string Model, string SerialNumber, string Port, string AdapterName);
