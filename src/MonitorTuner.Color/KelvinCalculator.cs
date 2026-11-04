namespace MonitorTuner.Color;

/// <summary>
/// Kelvin hesaplayıcı (T307). Hedef beyaz siyah cisim (Planck) eğrisinden gelir: ışıma → CIE 1931 XYZ → doğrusal sRGB,
/// 6500K'ya göre normalize edilir (6500K = tam nötr) ve en büyük kanal 1'e çekilir. Böylece renk kirli sarı değil,
/// doğal sıcak beyaz olur (08-GORUNTU-YONTEMLERI.md → 4). Çarpanlar doğrusal ışıktır.
/// </summary>
public sealed class KelvinCalculator : IKelvinCalculator
{
    /// <summary>Filtre kapalı: nötr.</summary>
    public const int OffKelvin = 6500;

    /// <summary>En sıcak sınır; mavi neredeyse sıfır.</summary>
    public const int MinKelvin = 1900;

    private const double SecondRadiation = 1.4388e-2; // hc/k, m·K

    private static readonly (double R, double G, double B) Reference = LinearRgb(OffKelvin);

    public KelvinSplit Calculate(int kelvin, RgbGains monitorFloor)
    {
        ValidateFloor(monitorFloor.R, nameof(monitorFloor));
        ValidateFloor(monitorFloor.G, nameof(monitorFloor));
        ValidateFloor(monitorFloor.B, nameof(monitorFloor));

        var target = Target(Math.Clamp(kelvin, MinKelvin, OffKelvin));

        // Önce monitör: hedefe inebildiği kadar iner (tabana kadar); kalanı GPU'ya kalır.
        var monitor = new RgbGains(
            Math.Max(target.R, monitorFloor.R), Math.Max(target.G, monitorFloor.G), Math.Max(target.B, monitorFloor.B));
        var gpu = new RgbGains(target.R / monitor.R, target.G / monitor.G, target.B / monitor.B);
        return new KelvinSplit(monitor, gpu);
    }

    private static RgbGains Target(int kelvin)
    {
        if (kelvin >= OffKelvin) return RgbGains.Neutral;

        var (r, g, b) = LinearRgb(kelvin);
        double[] c = [Math.Max(0, r / Reference.R), Math.Max(0, g / Reference.G), Math.Max(0, b / Reference.B)];
        var max = c.Max();
        return new RgbGains(c[0] / max, c[1] / max, c[2] / max);
    }

    private static (double R, double G, double B) LinearRgb(int kelvin)
    {
        double x = 0, y = 0, z = 0;
        for (var nm = 360; nm <= 830; nm++)
        {
            var m = nm * 1e-9;
            var radiance = 1 / (Math.Pow(m, 5) * (Math.Exp(SecondRadiation / (m * kelvin)) - 1));
            x += radiance * Lobe(nm, 1.056, 599.8, 37.9, 31.0) + radiance * Lobe(nm, 0.362, 442.0, 16.0, 26.7)
                 - radiance * Lobe(nm, 0.065, 501.1, 20.4, 26.2);
            y += radiance * (Lobe(nm, 0.821, 568.8, 46.9, 40.5) + Lobe(nm, 0.286, 530.9, 16.3, 31.1));
            z += radiance * (Lobe(nm, 1.217, 437.0, 11.8, 36.0) + Lobe(nm, 0.681, 459.0, 26.0, 13.8));
        }

        return (3.2406 * x - 1.5372 * y - 0.4986 * z,
            -0.9689 * x + 1.8758 * y + 0.0415 * z,
            0.0557 * x - 0.2040 * y + 1.0570 * z);
    }

    /// <summary>Wyman, Sloan, Shirley (2013): CIE 1931 eğrilerinin çok loblu Gauss yaklaşığı.</summary>
    private static double Lobe(double nm, double scale, double mean, double sigmaLow, double sigmaHigh)
    {
        var t = (nm - mean) / (nm < mean ? sigmaLow : sigmaHigh);
        return scale * Math.Exp(-0.5 * t * t);
    }

    private static void ValidateFloor(double value, string name)
    {
        if (!(value > 0 && value <= 1))
            throw new ArgumentOutOfRangeException(name, value, "Monitör tabanı (0, 1] aralığında olmalı.");
    }
}
