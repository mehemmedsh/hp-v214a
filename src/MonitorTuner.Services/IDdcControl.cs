using MonitorTuner.Core;

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

    /// <summary>
    /// Ham değer. AE/AC'nin Hz'e çevrilmesi ve AC'nin taşma düzeltmesi profilde (<c>FrequencyReadoutRules.RefreshHz</c>,
    /// <c>HorizontalHz</c>): düzeltilmiş AC 16 bite sığmaz. null: DDC yok ya da okunamadı.
    /// </summary>
    DdcValue? Read(byte code);

    DdcWriteResult Write(byte code, ushort value);
}
