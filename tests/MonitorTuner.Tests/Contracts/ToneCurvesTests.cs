using MonitorTuner.Color;
using MonitorTuner.Core;

namespace MonitorTuner.Tests.Contracts;

/// <summary>
/// Ton eğrisi sözleşmesi (T320, 02-MIMARI.md, 08-GORUNTU-YONTEMLERI.md → 6): stilden ton katmanı. Katman kalibrasyondan önce
/// uygulanır; kalibre panel göreli 2,2 gösterdiği için "göreli ışık x^γ" hedefi kodda x^(γ/2,2) demektir.
/// </summary>
public class ToneCurvesTests
{
    private static readonly ToneCurves Curves = new();

    [Fact]
    public void Standard_HasNoLayer() => Assert.Null(Curves.Build(ToneStyle.Standard));

    /// <summary>Siyah ve beyaz yerinde (parlaklık artmaz, siyah kalkmaz), eğri artan, üç kanal aynı (gri gri kalır).</summary>
    [Theory]
    [InlineData(ToneStyle.Cinema)]
    [InlineData(ToneStyle.Contrast)]
    public void Curves_KeepBlackAndWhite_RiseEverywhere_AndStayGrey(ToneStyle style)
    {
        var ramp = Curves.Build(style)!;

        Assert.Equal((ushort)0, ramp.Green[0]);
        Assert.Equal(ushort.MaxValue, ramp.Green[255]);
        for (var i = 1; i < GammaRamp.Size; i++)
            Assert.True(ramp.Green[i] > ramp.Green[i - 1], $"{style}: {i}. giriş bir öncekinden büyük değil");
        Assert.Equal(ramp.Green, ramp.Red);
        Assert.Equal(ramp.Green, ramp.Blue);
        Assert.NotEqual(GammaRamp.Identity, ramp);
    }

    /// <summary>Sinema: diz noktasından (64/255) itibaren tam gamma 2,4.</summary>
    [Theory]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(192)]
    [InlineData(230)]
    public void Cinema_FromTheKneeUp_IsGamma24(int level)
    {
        var expected = Math.Pow(level / 255.0, ToneCurves.CinemaGamma / CalibrationChoices.TargetGamma) * ushort.MaxValue;
        Assert.InRange(Curves.Build(ToneStyle.Cinema)!.Green[level], expected - 1, expected + 1);
    }

    /// <summary>
    /// Sinema orta tonları belirgin koyulaştırır (128 → ~120, 8 bit) ama siyaha yakın 13 basamak bir seviyeden az değişir:
    /// TN panelin gri siyahında ilk basamaklar zaten zor seçilir, eğri onları ezmez.
    /// </summary>
    [Fact]
    public void Cinema_DeepensMidtones_ButKeepsTheFirstStepsOutOfBlack()
    {
        var ramp = Curves.Build(ToneStyle.Cinema)!;

        Assert.InRange(ramp.Green[128] / 257.0, 119.5, 121);
        for (var i = 1; i <= 13; i++)
            Assert.InRange(ramp.Green[i] / 257.0, i - 1.0, i);
    }

    /// <summary>Geçiş bölgesi kırılmasız: komşu girişler arasındaki eğim farkı küçük (smoothstep).</summary>
    [Fact]
    public void Cinema_BlendsSmoothlyThroughTheKnee()
    {
        var ramp = Curves.Build(ToneStyle.Cinema)!;
        for (var i = 2; i < GammaRamp.Size; i++)
            Assert.True(Math.Abs(ramp.Green[i] - 2 * ramp.Green[i - 1] + ramp.Green[i - 2]) <= 12, $"{i}. girişte kırılma");
    }

    /// <summary>Canlı kontrast: orta gri yerinde; 0,1 → 0,091 (koyulaşır), 0,9 → 0,909 (açılır); eğim ortada 1,075, uçlarda 0,85.</summary>
    [Fact]
    public void Contrast_IsAGentleSCurveAroundMidGrey()
    {
        Assert.Equal(0.5, ToneCurves.Contrast(0.5), 12);
        Assert.Equal(0.0892, ToneCurves.Contrast(0.1), 4);
        Assert.Equal(0.9108, ToneCurves.Contrast(0.9), 4);

        var ramp = Curves.Build(ToneStyle.Contrast)!;
        var midSlope = (ramp.Green[128] - ramp.Green[127]) / 257.0;
        var blackSlope = (ramp.Green[1] - ramp.Green[0]) / 257.0;
        Assert.InRange(midSlope, 1.06, 1.09);
        Assert.InRange(blackSlope, 0.84, 0.87);
    }

    [Fact]
    public void UnknownStyle_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Curves.Build((ToneStyle)99));
}
