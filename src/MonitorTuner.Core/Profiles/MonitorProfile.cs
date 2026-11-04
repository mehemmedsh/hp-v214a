using System.Text.Json.Serialization;

namespace MonitorTuner.Core.Profiles;

/// <summary>
/// Bir Hz modunun denenme durumu. <see cref="NvidiaPassed"/>: NVIDIA'nın 15 sn'lik testi geçti ama AE ve kare atlama
/// doğrulaması bekliyor. <see cref="Verified"/>: AE ve kare atlama dahil tam doğrulandı. <see cref="Rejected"/>: monitör
/// "Out of Range" verdi.
/// </summary>
public enum ModeStatus { Untested, NvidiaPassed, Verified, Rejected }

/// <summary>Modun nereden geldiği: monitörün EDID'inde hazır mı, kullanıcı NVIDIA özel çözünürlüğüyle mi ekliyor.</summary>
public enum ModeSource { Edid, CustomResolution }

/// <summary>
/// Bir modun timing'i (NVIDIA özel çözünürlük penceresindeki değerler). Toplamlar, yatay frekans ve pixel clock buradan
/// hesaplanır; NVIDIA penceresindeki kHz alanı Hz değişince güncellenmediği için ona güvenilmez (LESSONS.md).
/// </summary>
public sealed record ModeTiming(
    int HActive, int HFrontPorch, int HSync, int HBackPorch,
    int VActive, int VFrontPorch, int VSync, int VBackPorch)
{
    public int HTotal => HActive + HFrontPorch + HSync + HBackPorch;
    public int VTotal => VActive + VFrontPorch + VSync + VBackPorch;

    /// <summary>Yatay frekans, kHz: Hz × dikey toplam.</summary>
    public double HorizontalKhz(int refreshHz) => refreshHz * (double)VTotal / 1000;

    /// <summary>Pixel clock, MHz: Hz × yatay toplam × dikey toplam.</summary>
    public double PixelClockMhz(int refreshHz) => refreshHz * (double)HTotal * VTotal / 1_000_000;
}

/// <summary>Profildeki bir Hz modu: timing, kaynak, test durumu ve not.</summary>
public sealed record ProfileMode(int RefreshHz, ModeStatus Status, ModeSource Source, ModeTiming Timing, string Note = "");

public sealed record PanelInfo(string Technology, int ColorBits, bool UsesFrc, bool HasPwm);

/// <summary>EDID'in aralık sınırları (spec). Scaler gerçekte biraz üstünü kabul eder: <see cref="MeasuredLimits"/>.</summary>
public sealed record EdidLimits(
    int VerticalMinHz, int VerticalMaxHz, int HorizontalMinKhz, int HorizontalMaxKhz, int MaxPixelClockMhz);

/// <summary>
/// Ünitede ölçülen sınır. Belirleyici olan yatay frekanstır: <see cref="HighestAcceptedHorizontalKhz"/> kabul edildi,
/// <see cref="LowestRejectedHorizontalKhz"/> ve üstü "Out of Range" verdi.
/// </summary>
public sealed record MeasuredLimits(
    int MaxRefreshHz, double HighestAcceptedHorizontalKhz, double LowestRejectedHorizontalKhz, string Note = "");

public enum OsdColorPreset { Warm, Neutral, Cool, Night, Native, LowBlueLight, Reading, HpEnhancePlus, CustomRgb }

/// <summary>OSD'den bilinen yetenekler.</summary>
/// <param name="ShowsIncomingRefreshRate">Alt çubuk her menüde gelen Hz'i gösterir.</param>
/// <param name="OverscanMustBeOff">Overscan Off kalmalı.</param>
/// <param name="RgbGainPreset">RGB kazancının etkili olduğu renk ön ayarı.</param>
public sealed record OsdCapabilities(
    bool HasOverdrive,
    bool ShowsIncomingRefreshRate,
    bool OverscanMustBeOff,
    IReadOnlyList<OsdColorPreset> ColorPresets,
    OsdColorPreset FactoryColorPreset,
    OsdColorPreset RgbGainPreset);

public enum DdcAccess { ReadOnly, ReadWrite, WriteOnly }

/// <summary>
/// DDC kod tablosundaki bir kod (ControlMyMonitor dökümünden). <paramref name="AppMayWrite"/>: uygulamanın yazabildiği
/// kod; sadece okunur olmayan, yasak olmayan (04/05/08, E6–FF) ve çalıştığı bilinen kodlar için true olabilir.
/// </summary>
public sealed record DdcCodeInfo(
    byte Code,
    string Name,
    DdcAccess Access,
    int? Maximum,
    IReadOnlyList<int> PossibleValues,
    bool AppMayWrite,
    string Note = "");

public enum MappingConfidence { Estimated, Confirmed }

/// <summary>14 kodunun bir değerinin OSD ön ayarı karşılığı. <see cref="Estimated"/> değer yazılmadan önce doğrulanmalıdır.</summary>
public sealed record ColorPresetMapping(int DdcValue, OsdColorPreset Preset, MappingConfidence Confidence);

/// <summary>Bir AE/AC okuması ve doğru karşılığı (T301 test vektörü).</summary>
public sealed record FrequencySample(int RefreshHz, int Ae, int Ac, int HorizontalHz);

/// <summary>
/// AE/AC okuma kuralları: AE = Hz × <see cref="VerticalDivisor"/>. AC yatay frekansı Hz cinsinden 16 bit taşmalı verir:
/// gerçek değer, okunan değere <see cref="HorizontalWrapModulus"/> katları eklenerek [<see cref="HorizontalMinHz"/>,
/// <see cref="HorizontalMaxHz"/>] aralığına düşen tek değerdir (T301). Düzeltilmiş değer 16 bite sığmadığı için
/// <c>IDdcControl.Read</c> ham değeri verir; çeviri buradadır.
/// </summary>
public sealed record FrequencyReadoutRules(
    int VerticalDivisor,
    int HorizontalWrapModulus,
    int HorizontalMinHz,
    int HorizontalMaxHz,
    IReadOnlyList<FrequencySample> Samples)
{
    /// <summary>AE okumasından gelen dikey frekans, Hz. null: okuma geçersiz (0 ya da negatif).</summary>
    public double? RefreshHz(int ae) => ae > 0 ? (double)ae / VerticalDivisor : null;

    /// <summary>AC okumasının taşma düzeltmesi: yatay frekans, Hz. null: aralığa düşen değer yok ya da birden fazla.</summary>
    public int? HorizontalHz(int ac)
    {
        if (ac < 0 || ac >= HorizontalWrapModulus) return null;
        int? found = null;
        for (long hz = ac; hz <= HorizontalMaxHz; hz += HorizontalWrapModulus)
        {
            if (hz < HorizontalMinHz) continue;
            if (found is not null) return null;
            found = (int)hz;
        }
        return found;
    }
}

/// <summary>
/// Monitör profili (veri dosyası, <c>/profiles</c>), <see cref="IMonitorProfileReader"/> okur ve doğrular.
/// </summary>
/// <param name="Modes">Kullanılabilir modlar, Hz başına bir tane (reddedilmiş mod yok); orijinal 60 Hz mutlaka vardır.</param>
/// <param name="RejectedModes">Monitörün reddettiği timing'ler; uygulama bunları önermez.</param>
/// <param name="SpecMaxRefreshHz">Yatay frekans ve pixel clock EDID sınırları içinde kalan en yüksek Hz (dikey 60 Hz sınırını scaler uygulamıyor).</param>
public sealed record MonitorProfile(
    int SchemaVersion,
    string Model,
    string EdidName,
    PanelInfo Panel,
    EdidLimits EdidLimits,
    int SpecMaxRefreshHz,
    MeasuredLimits Measured,
    IReadOnlyList<ProfileMode> Modes,
    IReadOnlyList<ProfileMode> RejectedModes,
    OsdCapabilities Osd,
    IReadOnlyList<DdcCodeInfo> DdcCodes,
    IReadOnlyList<ColorPresetMapping> ColorPresetMap,
    FrequencyReadoutRules FrequencyReadout)
{
    // Hesaplanan üyeler dosyaya yazılmaz, dosyadan okunmaz.
    [JsonIgnore]
    public bool HasPwm => Panel.HasPwm;

    [JsonIgnore]
    public int MeasuredMaxRefreshHz => Measured.MaxRefreshHz;

    /// <summary>Uygulamanın yazabildiği DDC kodları (Güvenlik katmanının izin listesi bundan kurulur).</summary>
    [JsonIgnore]
    public IReadOnlySet<byte> WritableDdcCodes =>
        DdcCodes.Where(c => c.AppMayWrite).Select(c => c.Code).ToHashSet();

    /// <summary>Mod, spec dışı mı (72 Hz üstü: ilk geçişte uyarı gösterilir, 03-GUVENLIK.md → 2).</summary>
    public bool IsBeyondSpec(ProfileMode mode) => mode.RefreshHz > SpecMaxRefreshHz;
}

/// <summary>Profil dosyası okunamadı ya da kuralları bozuyor.</summary>
public sealed class InvalidProfileException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Monitör profili modülü: profil dosyasını okur ve doğrular (saf mantık, donanımsız).</summary>
public interface IMonitorProfileReader
{
    /// <summary>Geçersiz profilde (ör. yazılabilir kodlarda 04/05/08 veya E6–FF) <see cref="InvalidProfileException"/> fırlatır.</summary>
    MonitorProfile Read(string json);
}
