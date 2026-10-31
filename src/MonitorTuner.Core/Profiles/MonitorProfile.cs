namespace MonitorTuner.Core.Profiles;

public enum ModeStatus { Untested, Works, Rejected }

/// <summary>Profildeki bir Hz modu. T004 timing alanlarını (NVIDIA özel çözünürlük değerleri) ekler.</summary>
public sealed record ProfileMode(int RefreshHz, ModeStatus Status);

/// <summary>
/// Monitör profili (veri dosyası, <c>/profiles</c>). T004 şemayı tamamlar: timing'ler, OSD yetenekleri,
/// 14 kodunun ön ayar eşlemesi, AE/AC okuma kuralları.
/// </summary>
public sealed record MonitorProfile(
    string Model,
    IReadOnlyList<ProfileMode> Modes,
    int SpecMaxRefreshHz,
    int MeasuredMaxRefreshHz,
    IReadOnlySet<byte> WritableDdcCodes,
    bool HasPwm);

/// <summary>Monitör profili modülü: profil dosyasını okur ve doğrular (saf mantık, donanımsız).</summary>
public interface IMonitorProfileReader
{
    /// <summary>Geçersiz profilde (ör. yazılabilir kodlarda 04/05/08 veya E6–FF) hata fırlatır.</summary>
    MonitorProfile Read(string json);
}
