using MonitorTuner.Color;
using MonitorTuner.Core;

namespace MonitorTuner.Services;

/// <summary>Renk profili. null alan: profil o ayara dokunmaz. Hz alanı yok: profiller Hz'e dokunmaz.</summary>
/// <param name="Brightness">Tek kaydırıcı (0–1): arka ışık + ek karartma.</param>
/// <param name="Kelvin">Mavi ışık; 6500 = kapalı.</param>
/// <param name="Vibrance">NVIDIA canlılık; sadece Film ve Oyun'da açık.</param>
public sealed record ColorProfile(string Name, bool BuiltIn, double? Brightness, int? Kelvin, int? Vibrance);

/// <summary>Renk profilleri (T308): sadece kullanıcı seçince, değerleri bir kez ayarlar.</summary>
public interface IColorProfiles
{
    /// <summary>Hazırlar (Gündüz, Akşam, Gece, Kod, Film, Oyun, DaVinci zaman çizelgesi) ve kullanıcınınkiler.</summary>
    IReadOnlyList<ColorProfile> Profiles { get; }

    void Apply(ColorProfile profile);

    void Save(ColorProfile profile);

    void Delete(string name);

    /// <summary>Panik kısayolu (T310): mavi ışık, ek karartma ve canlılık kapanır; kalibrasyon kalır.</summary>
    void PanicOff();
}

public enum WizardStep
{
    WarmUpAndPosition, BlackAndWhiteClipping, Sharpness, FullRgbCheck, ViewingAngle,
    DeadPixelsAndUniformity, WhiteMatch, Gamma, GreyBalance,
}

/// <summary>Temel ayar: sihirbazın DDC değerleri (kod → değer) ve kalibrasyon katmanı.</summary>
public sealed record Baseline(IReadOnlyDictionary<byte, ushort> DdcValues, GammaRamp Calibration);

/// <summary>Ayar sihirbazı (T312, T313). Başlarken efektler geçici kapanır, bitince ya da iptalde geri gelir.</summary>
public interface ISetupWizard
{
    /// <summary>Adımlar sırayla. T312: ısınma, kırpma, keskinlik, RGB Full, açı, ölü piksel, beyaz eşleme; Gamma ve GreyBalance T313'te eklenir.</summary>
    IReadOnlyList<WizardStep> Steps { get; }

    bool IsActive { get; }

    /// <summary>Isınma için kalan süre (Begin'den 30 dk). Engellemez; kullanıcı atlayabilir.</summary>
    TimeSpan WarmUpRemaining { get; }

    /// <summary>Mavi ışık ve ek karartma kapanır, DDC değerleri iptal için saklanır. Monitöre yazmaz.</summary>
    void Begin();

    /// <summary>
    /// Sihirbazın ayarlarından birini <see cref="IDdcPanel"/> üzerinden yazar (dijital parlaklık koruması, OSD değişikliği
    /// ve Custom RGB kuralları geçerli). Giriş ve ses sihirbazın değildir: <see cref="PanelSetResult.NotSupported"/>.
    /// </summary>
    PanelSetResult SetDdc(PanelSetting setting, int value);

    /// <summary>T313. T312'de desteklenmez.</summary>
    void SetCalibration(CalibrationChoices choices);

    /// <summary>Temel ayarı kaydeder ve efektleri geri getirir.</summary>
    Baseline Finish();

    /// <summary>Sihirbaz öncesine döner, efektleri geri getirir. Geri yazılamayan ayarları verir (boş: hepsi döndü).</summary>
    IReadOnlyList<PanelSetting> Cancel();

    /// <summary>Kayıtlı temel ayar; yoksa null.</summary>
    Baseline? LoadBaseline();
}

public sealed record RecoveryReport(bool GpuTableReset, bool GammaRangeRestored, bool DitheringReset, bool VibranceReset);

/// <summary>Kurtarma (T104): GPU tablosu, GdiIcmGammaRange, dithering ve canlılığı varsayılana döndürür.</summary>
public interface IRecovery
{
    RecoveryReport ResetToDefaults();
}
