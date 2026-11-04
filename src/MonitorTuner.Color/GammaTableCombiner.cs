using MonitorTuner.Core;

namespace MonitorTuner.Color;

/// <summary>
/// Tablo birleştirme (T302): katmanlar aktarım fonksiyonu olarak sırayla uygulanır, sonuç(x) = Lₙ(…L₂(L₁(x))).
/// Önceki katmanın çıktısı sonraki katmanın 256 girişinde doğrusal aradeğerlemeyle aranır ve ara sonuç ondalıklı
/// kalır; yuvarlama sadece en sonda bir kez yapılır, böylece katmanlar arasında seviye kaybı birikmez.
/// </summary>
public sealed class GammaTableCombiner : IGammaTableCombiner
{
    public GammaRamp Combine(IReadOnlyList<GammaRamp> layersInOrder)
    {
        ArgumentNullException.ThrowIfNull(layersInOrder);
        double[] red = Start(), green = Start(), blue = Start();
        foreach (var layer in layersInOrder)
        {
            ArgumentNullException.ThrowIfNull(layer, nameof(layersInOrder));
            Through(red, layer.Red);
            Through(green, layer.Green);
            Through(blue, layer.Blue);
        }
        return new GammaRamp(Encode(red), Encode(green), Encode(blue));
    }

    /// <summary>Birim tablo: giriş i → i × 257.</summary>
    private static double[] Start() => [.. Enumerable.Range(0, GammaRamp.Size).Select(i => i * 257.0)];

    private static void Through(double[] values, IReadOnlyList<ushort> table)
    {
        for (var i = 0; i < values.Length; i++) values[i] = Lookup(table, values[i]);
    }

    /// <summary>16 bit değer v, tablonun girişleri arasında (i = v / 257) doğrusal aradeğerlemeyle.</summary>
    private static double Lookup(IReadOnlyList<ushort> table, double value)
    {
        var position = Math.Clamp(value, 0, ushort.MaxValue) / 257.0;
        var low = (int)position;
        if (low >= GammaRamp.Size - 1) return table[GammaRamp.Size - 1];
        var fraction = position - low;
        return table[low] + (table[low + 1] - table[low]) * fraction;
    }

    private static ushort[] Encode(double[] values) =>
        [.. values.Select(v => (ushort)Math.Clamp(Math.Round(v), 0, ushort.MaxValue))];
}
