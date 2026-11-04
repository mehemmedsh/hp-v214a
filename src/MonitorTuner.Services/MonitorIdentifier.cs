using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;

namespace MonitorTuner.Services;

/// <summary>
/// Monitör tanıma (T102): bağlı ekranların EDID'sini çözer; bağlantı tipi, port ve kart donanım yarısından (<see cref="IDisplayEnumerator"/>) gelir.
/// Okunamayan EDID'li ekran listeden düşmez; modeli ve seri no'su boş kalır, böylece hiçbir profille eşleşmez ve
/// ona yazma yapılmaz.
/// </summary>
public sealed class MonitorIdentifier(IDisplayEnumerator displays) : IMonitorIdentifier
{
    public IReadOnlyList<MonitorInfo> Identify() =>
        displays.EnumerateDisplays().Select(Describe).ToList();

    private static MonitorInfo Describe(ConnectedDisplay display)
    {
        var edid = EdidInfo.TryParse(display.Edid);
        return new MonitorInfo(
            display.Display,
            edid?.ModelName ?? "",
            edid?.SerialText ?? "",
            display.Connection,
            display.Port,
            display.AdapterName);
    }
}
