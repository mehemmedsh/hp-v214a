using MonitorTuner.Color;
using MonitorTuner.Core;

namespace MonitorTuner.Services;

/// <summary>
/// Renk profili. Hz alanı yok: profiller Hz'e dokunmaz. null <c>Brightness</c> ve <c>Kelvin</c>: profil o ayara dokunmaz.
/// null <c>Vibrance</c>: canlılık yok; uygulamanın açtığı canlılık kapanır (kullanıcının kendi değerine dokunulmaz).
/// <c>Tone</c> her profilde vardır (T320): eski kayıtlarda alan yoksa <see cref="ToneStyle.Standard"/> (ton katmanı kapanır).
/// </summary>
/// <param name="Brightness">Tek kaydırıcı (0–1): arka ışık + ek karartma.</param>
/// <param name="Kelvin">Mavi ışık; 6500 = kapalı.</param>
/// <param name="Vibrance">NVIDIA canlılık (50–100; NVIDIA panelindeki %).</param>
/// <param name="Tone">Ton eğrisi (T320): Standart (kalibrasyonun 2,2'si), Sinema (2,4) ya da canlı kontrast.</param>
/// <param name="Group">Arayüzdeki grubu (T321); kullanıcı profilleri her zaman <see cref="ProfileGroup.User"/>.</param>
public sealed record ColorProfile(
    string Name, bool BuiltIn, double? Brightness, int? Kelvin, int? Vibrance, ToneStyle Tone = ToneStyle.Standard,
    ProfileGroup Group = ProfileGroup.User);

/// <summary>
/// Modların grubu (T321). Arayüzde ve tepside <c>ColorProfiles.GroupOrder</c> sırasıyla. <see cref="User"/> = 0: eski
/// kayıtlarda alan yoksa da kullanıcı grubu gelir.
/// </summary>
public enum ProfileGroup
{
    /// <summary>Kullanıcının kaydettiği profiller.</summary>
    User = 0,

    /// <summary>Film ve dizi: oda ışığına göre sinema modları.</summary>
    Cinema = 1,

    /// <summary>Uzun kullanım, göz konforu: efektsiz, ılımlı sıcaklık, oda ışığına uygun parlaklık.</summary>
    Comfort = 2,

    /// <summary>Günlük: Gündüz (doğru renk), Akşam ve Gece (uyku öncesi, güçlü mavi ışık azaltma), Kod.</summary>
    Daily = 3,

    /// <summary>Özel işler: Oyun, DaVinci zaman çizelgesi.</summary>
    Special = 4,
}

/// <summary>Renk profilleri (T308): sadece kullanıcı seçince, değerleri bir kez ayarlar.</summary>
public interface IColorProfiles
{
    /// <summary>Hazırlar (grup sırasıyla: sinema, uzun kullanım, günlük, özel; T321) ve kullanıcınınkiler.</summary>
    IReadOnlyList<ColorProfile> Profiles { get; }

    /// <summary>
    /// Kullanıcı seçince: parlaklık, mavi ışık, ton eğrisi, canlılık sırayla, her biri bir kez (kendi servisi üzerinden). Hz'e ve
    /// kalibrasyona dokunmaz. Geçersiz profilde <see cref="ArgumentException"/>, hiçbir şey yazılmadan.
    /// </summary>
    ProfileApplyResult Apply(ColorProfile profile);

    /// <summary>Kullanıcı profili ekler ya da aynı adlıyı değiştirir. Hazır adlar kullanılamaz (<see cref="ArgumentException"/>).</summary>
    void Save(ColorProfile profile);

    /// <summary>Kullanıcı profilini siler. Hazır profil silinemez (<see cref="InvalidOperationException"/>); olmayan ad sessizce geçilir.</summary>
    void Delete(string name);

    /// <summary>Panik kısayolu (T310): mavi ışık, ek karartma, ton eğrisi (T320) ve canlılık kapanır; kalibrasyon kalır.</summary>
    void PanicOff();
}

public enum WizardStep
{
    WarmUpAndPosition, BlackAndWhiteClipping, Sharpness, FullRgbCheck, ViewingAngle,
    DeadPixelsAndUniformity, WhiteMatch, Gamma, GreyBalance,
}

/// <summary>Temel ayar: sihirbazın DDC değerleri (kod → değer) ve kalibrasyon katmanı.</summary>
public sealed record Baseline(IReadOnlyDictionary<byte, ushort> DdcValues, GammaRamp Calibration);

/// <summary>Ayar sihirbazı (T312, T313). Başlarken efektler (mavi ışık, ek karartma, ton eğrisi, canlılık) geçici kapanır, bitince ya da iptalde geri gelir.</summary>
public interface ISetupWizard
{
    /// <summary>Adımlar sırayla: ısınma, kırpma, keskinlik, RGB Full, açı, ölü piksel, beyaz eşleme, gamma, gri dengesi.</summary>
    IReadOnlyList<WizardStep> Steps { get; }

    bool IsActive { get; }

    /// <summary>Isınma için kalan süre (Begin'den 30 dk). Engellemez; kullanıcı atlayabilir.</summary>
    TimeSpan WarmUpRemaining { get; }

    /// <summary>
    /// Mavi ışık, ek karartma, ton eğrisi ve canlılık kapanır, DDC değerleri iptal için saklanır. Monitöre (DDC) yazmaz. Eski kalibrasyon
    /// varsa ekran ham panele döner (gamma ve gri dengesi düzeltilmemiş panelde ölçülür); Finish ya da Cancel geri getirir.
    /// </summary>
    void Begin();

    /// <summary>
    /// Sihirbazın ayarlarından birini <see cref="IDdcPanel"/> üzerinden yazar (dijital parlaklık koruması, OSD değişikliği
    /// ve Custom RGB kuralları geçerli). Giriş ve ses sihirbazın değildir: <see cref="PanelSetResult.NotSupported"/>.
    /// </summary>
    PanelSetResult SetDdc(PanelSetting setting, int value);

    /// <summary>
    /// Seçimlerden kalibrasyon katmanını üretir ve anında ekrana verir (GPU; DDC'ye dokunmaz). DDC yokken beyaz eşleme de
    /// buradan (<see cref="CalibrationChoices.WhiteGains"/>). Her çağrı bütün kalibrasyonu yeniden kurar. false: Windows tabloyu
    /// reddetti (ekranda ve kayıtta önceki kalibrasyon kalır). Seçim sınır dışıysa <see cref="ArgumentOutOfRangeException"/>, hiçbir şey yazılmaz.
    /// </summary>
    bool SetCalibration(CalibrationChoices choices);

    /// <summary>Temel ayarı (DDC değerleri ve kalibrasyon) kaydeder ve efektleri geri getirir.</summary>
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
