using MonitorTuner.Core;

namespace MonitorTuner.Color;

/// <summary>
/// Ton eğrisi (T320): içeriğin orta tonlarının ekranda nasıl duracağı. Kalibrasyon paneli göreli gamma 2,2'ye getirir
/// (<see cref="CalibrationChoices.TargetGamma"/>); ton eğrisi bunun üstüne bir "bakış" seçer. Değer kayıtta ad olarak durur
/// (ayar deposu enum'u metin yazar); <see cref="Standard"/> = 0, eski kayıtlarda alan yoksa da Standard gelir.
/// </summary>
public enum ToneStyle
{
    /// <summary>Kalibrasyonun kendisi (sRGB masaüstü, aydınlık oda): ton katmanı yok.</summary>
    Standard = 0,

    /// <summary>
    /// Sinema: orta ve açık tonlarda gamma 2,4 (BT.1886, filmlerin hazırlandığı loş ve karanlık oda); siyaha yakın tonlarda 2,2'ye
    /// yumuşak geçiş, böylece TN panelin gri siyahında gölge detayı ezilmez (08-GORUNTU-YONTEMLERI.md → 6).
    /// </summary>
    Cinema = 1,

    /// <summary>Canlı kontrast: orta griyi sabit tutan hafif S eğrisi; koyular biraz koyulaşır, açıklar biraz açılır, siyah ve beyaz yerinde.</summary>
    Contrast = 2,
}

/// <summary>Ton eğrisi hesaplayıcı (T320): stilden GPU tablosunun ton katmanını üretir.</summary>
public interface IToneCurves
{
    /// <summary>null: katman yok (<see cref="ToneStyle.Standard"/>). Bilinmeyen stil <see cref="ArgumentOutOfRangeException"/>.</summary>
    GammaRamp? Build(ToneStyle style);
}

/// <summary>
/// Ton eğrileri (T320). Katman kalibrasyondan <b>önce</b> uygulanır (renk boru hattı sırası: ton → kalibrasyon → mavi ışık →
/// ek karartma): içerik değeri x önce ton eğrisinden geçer, kalibrasyon sonucu panelde göreli 2,2 ile gösterir. Böylece hedef
/// "göreli ışık = x^γ" ise ton çıkışı x^(γ/2,2) olur; gri dengesi bozulmaz, çünkü üç kanala aynı eğri uygulanır ve kanal
/// farkları sonra kalibrasyonda düzeltilir. Eğriler artan, siyah ve beyaz yerinde; panelin parlaklığını artırmaz (dijital
/// parlaklık koruması), sadece ara tonları dağıtır. Saf matematik; donanım yok.
/// </summary>
public sealed class ToneCurves : IToneCurves
{
    /// <summary>Sinema eğrisinin orta ve açık ton gamması (ITU-R BT.1886'nın üssü).</summary>
    public const double CinemaGamma = 2.4;

    /// <summary>Bu kodun altında gamma 2,2'den 2,4'e yumuşak geçiş (64/255 ≈ %25): gölge detayının asıl yeri.</summary>
    public const double ShadowKnee = 0.25;

    /// <summary>Canlı kontrast eğrisinin gücü: orta tonda eğim 1 + s/2 (+%7,5), siyah ve beyaz yanında 1 − s (%85).</summary>
    public const double ContrastStrength = 0.15;

    public GammaRamp? Build(ToneStyle style) => style switch
    {
        ToneStyle.Standard => null,
        ToneStyle.Cinema => Table(Cinema),
        ToneStyle.Contrast => Table(Contrast),
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Bilinmeyen ton stili."),
    };

    /// <summary>
    /// Sinema eğrisi, kodlanmış değer (0–1): x^(γ(x)/2,2), γ(x) siyahta 2,2, <see cref="ShadowKnee"/> ve üstünde 2,4 (arada
    /// smoothstep). Geçiş bölgesinde de artandır: d/dx[γ(x)·ln x] = γ'(x)·ln x + γ(x)/x, ikinci terim her yerde baskın.
    /// </summary>
    public static double Cinema(double x)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;
        var gamma = CalibrationChoices.TargetGamma + (CinemaGamma - CalibrationChoices.TargetGamma) * SmoothStep(x / ShadowKnee);
        return Math.Pow(x, gamma / CalibrationChoices.TargetGamma);
    }

    /// <summary>Canlı kontrast, kodlanmış değer (0–1): (1 − s)·x + s·(3x² − 2x³); 0,5 yerinde, eğim en az 1 − s.</summary>
    public static double Contrast(double x)
    {
        x = Math.Clamp(x, 0, 1);
        return (1 - ContrastStrength) * x + ContrastStrength * x * x * (3 - 2 * x);
    }

    private static double SmoothStep(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }

    /// <summary>Üç kanal aynı eğri: gri gri kalır.</summary>
    private static GammaRamp Table(Func<double, double> curve)
    {
        var channel = new ushort[GammaRamp.Size];
        for (var i = 0; i < channel.Length; i++)
            channel[i] = (ushort)Math.Clamp(Math.Round(curve(i / (double)(GammaRamp.Size - 1)) * ushort.MaxValue), 0, ushort.MaxValue);
        return new GammaRamp(channel, channel, channel);
    }
}
