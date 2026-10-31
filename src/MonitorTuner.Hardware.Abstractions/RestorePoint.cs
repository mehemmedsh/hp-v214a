using MonitorTuner.Core;

namespace MonitorTuner.Hardware.Abstractions;

public enum WriteTarget { Ddc, GpuGammaTable, DisplayMode, NvidiaSettings, GdiIcmGammaRange }

/// <summary>Güvenlik katmanına giden yazma isteği: neye, hangi ekranda (DDC'de hangi koda) yazılacak.</summary>
public sealed record WriteRequest(WriteTarget Target, DisplayId Display, byte? DdcCode = null);

/// <summary>
/// Geri alma noktası. Kurucusu <c>internal</c>: sadece Güvenlik katmanı (InternalsVisibleTo) oluşturur, önce
/// yedek alarak. Her donanım yazma metodu bunu parametre olarak ister; Güvenlik'i atlayan yazma derlenmez.
/// </summary>
public sealed class RestorePoint
{
    internal RestorePoint(Guid id, WriteRequest request, DateTimeOffset createdAt)
    {
        Id = id;
        Request = request;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public WriteRequest Request { get; }
    public DateTimeOffset CreatedAt { get; }
}
