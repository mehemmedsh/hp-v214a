using MonitorTuner.Core;

namespace MonitorTuner.Color;

/// <summary>
/// Gözle kalibrasyon eğrileri (T313). Panel c kodunu c^γp olarak gösterir; hedef x kodunda x^2,2 ışık. Bu yüzden kodlanmış
/// çıkış c = (x^2,2 · g(x))^(1/γp): γp = 2,2 ve g = 1 ise birim tablo. g(x), doğrusal ışık çarpanıdır: koyu, orta ve açık
/// gri seviyelerindeki düzeltmeler arasında (siyah ve beyazda 1) smoothstep ile yumuşak geçiş, hepsi beyaz kazancıyla çarpılır.
/// Çıkış kanal başına artan tutulur; küçük ince ayar için seçimler sınırlıdır (aşırı seçim <see cref="ArgumentOutOfRangeException"/>).
/// </summary>
public sealed class CalibrationCurves : ICalibrationCurves
{
    /// <summary>Gri dengesi desenlerinin seviyeleri (%20, %50, %80; <c>WizardPatterns</c> aynı sayıları gösterir).</summary>
    public const int DarkLevel = 51, MidLevel = 128, LightLevel = 204;

    public const double MinPanelGamma = 1.8, MaxPanelGamma = 2.8;
    public const double MinGreyGain = 0.8, MaxGreyGain = 1.25;
    public const double MinWhiteGain = 0.8;

    private static readonly double[] KnotX = [0, DarkLevel / 255.0, MidLevel / 255.0, LightLevel / 255.0, 1];

    public GammaRamp Build(CalibrationChoices choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        Validate(choices);
        var (dark, mid, light, white) = (choices.DarkGrey, choices.MidGrey, choices.LightGrey, choices.White);
        return new GammaRamp(
            Channel(choices.PanelGamma, white.R, dark.R, mid.R, light.R),
            Channel(choices.PanelGamma, white.G, dark.G, mid.G, light.G),
            Channel(choices.PanelGamma, white.B, dark.B, mid.B, light.B));
    }

    private static ushort[] Channel(double panelGamma, double white, double dark, double mid, double light)
    {
        double[] knotGain = [1, dark, mid, light, 1];
        var table = new ushort[GammaRamp.Size];
        var floor = 0.0; // artan kalması için
        for (var i = 0; i < table.Length; i++)
        {
            var x = i / (double)(GammaRamp.Size - 1);
            var linear = Math.Pow(x, CalibrationChoices.TargetGamma) * white * Gain(x, knotGain);
            floor = Math.Max(floor, Math.Pow(linear, 1 / panelGamma));
            table[i] = (ushort)Math.Clamp(Math.Round(floor * ushort.MaxValue), 0, ushort.MaxValue);
        }
        return table;
    }

    /// <summary>Düğüm noktaları arasında smoothstep: türev düğümlerde sürekli, aşım yok.</summary>
    private static double Gain(double x, double[] knotGain)
    {
        var k = 0;
        while (k < KnotX.Length - 2 && x > KnotX[k + 1]) k++;
        var t = Math.Clamp((x - KnotX[k]) / (KnotX[k + 1] - KnotX[k]), 0, 1);
        return knotGain[k] + (knotGain[k + 1] - knotGain[k]) * (t * t * (3 - 2 * t));
    }

    private static void Validate(CalibrationChoices c)
    {
        Require(c.PanelGamma, MinPanelGamma, MaxPanelGamma, nameof(c.PanelGamma));
        foreach (var (name, g) in new[] { (nameof(c.DarkGrey), c.DarkGrey), (nameof(c.MidGrey), c.MidGrey), (nameof(c.LightGrey), c.LightGrey) })
        {
            Require(g.R, MinGreyGain, MaxGreyGain, name);
            Require(g.G, MinGreyGain, MaxGreyGain, name);
            Require(g.B, MinGreyGain, MaxGreyGain, name);
        }
        Require(c.White.R, MinWhiteGain, 1, nameof(c.WhiteGains));
        Require(c.White.G, MinWhiteGain, 1, nameof(c.WhiteGains));
        Require(c.White.B, MinWhiteGain, 1, nameof(c.WhiteGains));
    }

    /// <summary>NaN de reddedilir.</summary>
    private static void Require(double value, double min, double max, string name)
    {
        if (!(value >= min && value <= max))
            throw new ArgumentOutOfRangeException(name, value, $"{min} – {max} aralığında olmalı.");
    }
}
