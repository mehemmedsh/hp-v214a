using MonitorTuner.Core;

namespace MonitorTuner.Hardware.Abstractions;

/// <summary>Sistemdeki bağlı ekranın ham bilgisi; EDID'yi çözmek monitör tanımanın işi (T102).</summary>
public sealed record ConnectedDisplay(DisplayId Display, byte[] Edid, ConnectionType Connection, string Port, string AdapterName);

/// <summary>Monitör tanımanın donanım yarısı: bağlı ekranları listeler. Yazmaz.</summary>
public interface IDisplayEnumerator
{
    IReadOnlyList<ConnectedDisplay> EnumerateDisplays();
}
