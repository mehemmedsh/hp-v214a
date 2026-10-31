using MonitorTuner.Core;

namespace MonitorTuner.Hardware.Abstractions;

/// <summary>DDC/CI'dan okunan değer (VCP kodu: şu anki ve en büyük değer).</summary>
public readonly record struct DdcValue(ushort Current, ushort Maximum);

/// <summary>
/// DDC katmanının donanım yarısı (ham DDC/CI). İzin listesi ve yazma sınırı burada değil, üstteki
/// <c>IDdcControl</c> (Services) ve Güvenlik katmanında.
/// </summary>
public interface IDdcMonitor
{
    /// <summary>Yetenek dizesi; null ise monitörde DDC/CI yok.</summary>
    string? ReadCapabilities(DisplayId display);

    /// <summary>null: okunamadı.</summary>
    DdcValue? Read(DisplayId display, byte code);

    /// <returns>Monitör yazmayı kabul ettiyse true.</returns>
    bool Write(DisplayId display, byte code, ushort value, RestorePoint restorePoint);
}
