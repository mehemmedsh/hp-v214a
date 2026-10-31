using MonitorTuner.Hardware.Abstractions;

namespace MonitorTuner.Services;

public enum DdcWriteResult { Written, NoDdc, Forbidden, Throttled, Failed }

/// <summary>
/// DDC katmanı (T301): yetenek testi, okuma/yazma, izin listesi (profil ∩ yetenek; 04/05/08 ve E6–FF asla),
/// yazma sınırı. Arayüz kaydırıcı bırakılınca bir kez <see cref="Write"/> çağırır.
/// </summary>
public interface IDdcControl
{
    bool IsAvailable { get; }

    IReadOnlySet<byte> WritableCodes { get; }

    /// <summary>AC gibi taşan değerler düzeltilmiş döner. null: okunamadı.</summary>
    DdcValue? Read(byte code);

    DdcWriteResult Write(byte code, ushort value);
}
