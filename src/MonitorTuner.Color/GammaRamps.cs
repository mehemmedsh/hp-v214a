using MonitorTuner.Core;

namespace MonitorTuner.Color;

/// <summary>GPU tablosu katmanları için yardımcılar (tablo birleştirmeyi T302 yazar).</summary>
public static class GammaRamps
{
    /// <summary>
    /// Doğrusal ışık çarpanlarından aktarım tablosu: doğrusal ışığı g ile çarpmak, kodlanmış değeri g^(1/γ) ile
    /// çarpmaktır (γ = 2.2). Nötr çarpanlar birim tabloyu verir.
    /// </summary>
    public static GammaRamp Scale(RgbGains linearGains)
    {
        return new GammaRamp(Channel(linearGains.R), Channel(linearGains.G), Channel(linearGains.B));

        static ushort[] Channel(double gain)
        {
            var encoded = EncodedGain(gain);
            var table = new ushort[GammaRamp.Size];
            for (var i = 0; i < table.Length; i++)
                table[i] = (ushort)Math.Clamp(Math.Round(i * 257 * encoded), 0, ushort.MaxValue);
            return table;
        }
    }

    /// <summary>Doğrusal ışık çarpanının kodlanmış değerdeki karşılığı.</summary>
    public static double EncodedGain(double linearGain) =>
        Math.Pow(Math.Clamp(linearGain, 0, 1), 1 / CalibrationChoices.TargetGamma);
}
