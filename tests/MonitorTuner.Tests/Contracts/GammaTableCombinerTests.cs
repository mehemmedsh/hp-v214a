using MonitorTuner.Color;
using MonitorTuner.Core;

namespace MonitorTuner.Tests.Contracts;

/// <summary>Tablo birleştirme (T302): katmanlar aktarım fonksiyonu olarak sırayla uygulanır.</summary>
public class GammaTableCombinerTests
{
    private static readonly GammaTableCombiner Combiner = new();
    private static readonly GammaRamp Curve = Power(0.8);

    [Fact]
    public void NoLayers_GiveIdentity() => Assert.Equal(GammaRamp.Identity, Combiner.Combine([]));

    [Fact]
    public void IdentityLayers_ChangeNothing()
    {
        Assert.Equal(Curve, Combiner.Combine([Curve]));
        Assert.Equal(Curve, Combiner.Combine([GammaRamp.Identity, Curve, GammaRamp.Identity]));
    }

    /// <summary>Sonuç(x) = sonraki(önceki(x)): kalibrasyondan sonra gelen ölçek kalibrasyonun çıktısını çarpar.</summary>
    [Fact]
    public void Layers_AreAppliedInGivenOrder()
    {
        var half = GammaRamps.Scale(new RgbGains(0.5, 0.5, 0.5));
        var gain = GammaRamps.EncodedGain(0.5);

        var calibrationThenScale = Combiner.Combine([Curve, half]);

        for (var i = 0; i < GammaRamp.Size; i++)
            Assert.InRange(calibrationThenScale.Red[i] - Curve.Red[i] * gain, -1, 1);
        Assert.NotEqual(Combiner.Combine([half, Curve]), calibrationThenScale);
    }

    /// <summary>Ara sonuç 8 bite yuvarlanmaz: iki ölçek tek ölçekle aynı tabloyu verir (en fazla 1 birim fark).</summary>
    [Fact]
    public void Scales_Multiply_WithoutLosingLevels()
    {
        var combined = Combiner.Combine([GammaRamps.Scale(new RgbGains(0.8, 0.8, 0.8)), GammaRamps.Scale(new RgbGains(0.5, 0.5, 0.5))]);
        var direct = GammaRamps.Scale(new RgbGains(0.4, 0.4, 0.4));

        for (var i = 0; i < GammaRamp.Size; i++)
            Assert.InRange(combined.Green[i] - direct.Green[i], -1, 1);
    }

    [Fact]
    public void Channels_StaySeparate()
    {
        var combined = Combiner.Combine([GammaRamps.Scale(new RgbGains(1, 1, 0.5)), GammaRamps.Scale(new RgbGains(0.5, 1, 1))]);
        var half = GammaRamps.Scale(new RgbGains(0.5, 0.5, 0.5));

        Assert.Equal(half.Red, combined.Red);
        Assert.Equal(GammaRamp.Identity.Green, combined.Green);
        Assert.Equal(half.Blue, combined.Blue);
    }

    private static GammaRamp Power(double exponent)
    {
        var t = Enumerable.Range(0, GammaRamp.Size).Select(i => (ushort)Math.Round(65535 * Math.Pow(i / 255.0, exponent))).ToArray();
        return new GammaRamp(t, t, t);
    }
}
