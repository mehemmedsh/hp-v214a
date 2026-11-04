using MonitorTuner.Core;

namespace MonitorTuner.Color;

/// <summary>Kanal çarpanları, doğrusal ışık; 1 = değişiklik yok.</summary>
public readonly record struct RgbGains(double R, double G, double B)
{
    public static RgbGains Neutral => new(1, 1, 1);
}

/// <param name="Monitor">Monitörün RGB kazancına (DDC) giden kısım.</param>
/// <param name="Gpu">Kalanı; GPU tablosunun mavi ışık katmanına gider.</param>
public readonly record struct KelvinSplit(RgbGains Monitor, RgbGains Gpu);

/// <summary>Kelvin hesaplayıcı (T307): 6500K (kapalı) → 1900K.</summary>
public interface IKelvinCalculator
{
    /// <param name="monitorFloor">Monitör kazancının inebileceği en düşük çarpanlar; DDC yoksa <see cref="RgbGains.Neutral"/>.</param>
    KelvinSplit Calculate(int kelvin, RgbGains monitorFloor);
}

/// <param name="Backlight">DDC parlaklık (0–100).</param>
/// <param name="GpuScale">GPU tablosu ölçeği; en az <see cref="SafetyLimits.MinWhiteLevel"/>.</param>
public readonly record struct DimmingPlan(int Backlight, double GpuScale);

/// <summary>Karartma planlayıcı (T306): tek kaydırıcı, önce arka ışık, sonra GPU.</summary>
public interface IDimmingPlanner
{
    /// <param name="slider">0 (en karanlık) – 1 (en parlak).</param>
    DimmingPlan Plan(double slider);
}

/// <summary>
/// Kullanıcının desen seçimleri: çizgili alandan okunan panel gamması ve üç gri seviyesindeki düzeltme (doğrusal ışık çarpanları).
/// <paramref name="WhiteGains"/>: monitörün DDC'si yoksa beyaz eşlemenin GPU'daki karşılığı (bütün seviyeleri çarpar; sadece kısar); null: dokunma.
/// </summary>
public sealed record CalibrationChoices(
    double PanelGamma, RgbGains DarkGrey, RgbGains MidGrey, RgbGains LightGrey, RgbGains? WhiteGains = null)
{
    public const double TargetGamma = 2.2;

    public RgbGains White => WhiteGains ?? RgbGains.Neutral;
}

/// <summary>Gözle kalibrasyon (T313): seçimlerden kalibrasyon katmanını üretir.</summary>
public interface ICalibrationCurves
{
    GammaRamp Build(CalibrationChoices choices);
}

/// <summary>Tablo birleştirme (T302): katmanları sırayla üst üste uygular, tek tablo üretir.</summary>
public interface IGammaTableCombiner
{
    /// <param name="layersInOrder">Kalibrasyon → mavi ışık → ek karartma; her katman bir aktarım tablosu.</param>
    GammaRamp Combine(IReadOnlyList<GammaRamp> layersInOrder);
}
