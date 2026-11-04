using MonitorTuner.Color;
using MonitorTuner.Core;

namespace MonitorTuner.Tests.Contracts;

/// <summary>Gözle kalibrasyon sözleşmesi (T313, 02-MIMARI.md): seçimlerden kalibrasyon katmanı (aktarım tablosu) üretir.</summary>
public class CalibrationCurvesTests
{
    private static readonly RgbGains N = RgbGains.Neutral;
    private static readonly CalibrationCurves Curves = new();

    private static CalibrationChoices Neutral(double gamma = 2.2, RgbGains? white = null) => new(gamma, N, N, N, white);

    private static IReadOnlyList<ushort> Channel(GammaRamp ramp, char channel) =>
        channel switch { 'R' => ramp.Red, 'G' => ramp.Green, _ => ramp.Blue };

    /// <summary>Panel gamması 2.2 ve gri dengesi nötrse eğri birim tablodur.</summary>
    [Fact]
    public void Build_NeutralChoices_GivesIdentity() =>
        Assert.Equal(GammaRamp.Identity, Curves.Build(Neutral()));

    /// <summary>Her kanalın eğrisi artan ve yumuşaktır (en uç seçimlerde de).</summary>
    [Fact]
    public void Build_IsMonotonic_PerChannel()
    {
        var extremes = new[]
        {
            Neutral(1.8), Neutral(2.8),
            new CalibrationChoices(2.6, new(1.25, 0.8, 1.25), new(0.8, 1.25, 0.8), new(1.25, 0.8, 1.25), new(0.8, 0.9, 1)),
            new CalibrationChoices(1.9, new(0.8, 1.25, 0.8), new(1.25, 0.8, 1.25), new(0.8, 1.25, 0.8), new(1, 1, 0.8)),
        };

        foreach (var choices in extremes)
        {
            var ramp = Curves.Build(choices);
            foreach (var c in "RGB")
            {
                var v = Channel(ramp, c);
                for (var i = 1; i < v.Count; i++) Assert.True(v[i] >= v[i - 1], $"{c} kanalı {i}. girişte azalıyor ({choices})");
                // Yumuşak: ikinci fark küçük (siyaha en yakın girişlerde us eğrisi dik olduğundan 8'den başlar).
                for (var i = 9; i < v.Count; i++)
                    Assert.True(Math.Abs(v[i] - 2 * v[i - 1] + v[i - 2]) <= 80, $"{c} kanalı {i}. girişte kırılıyor ({choices})");
            }
        }
    }

    /// <summary>Panel gamması hedeften yüksekse (karanlık) orta tonlar açılır, düşükse kısılır; uçlar yerinde.</summary>
    [Theory]
    [InlineData(2.6, true)]
    [InlineData(2.0, false)]
    public void Build_PanelGamma_BendsTheMidtonesTowardsTarget(double panelGamma, bool brighter)
    {
        var ramp = Curves.Build(Neutral(panelGamma));

        Assert.Equal(brighter, ramp.Green[128] > GammaRamp.Identity.Green[128]);
        Assert.Equal((ushort)0, ramp.Green[0]);
        Assert.Equal((ushort)65535, ramp.Green[255]);
        // Panel c^γp gösterir; c = x^(2.2/γp) olunca ışık x^2.2 olur.
        var expected = Math.Pow(128 / 255.0, CalibrationChoices.TargetGamma / panelGamma) * 65535;
        Assert.InRange(ramp.Green[128], expected - 1, expected + 1);
    }

    /// <summary>Gri dengesi sadece o kanalı ve o gri civarını kaydırır; siyah ve beyaz değişmez.</summary>
    [Fact]
    public void Build_GreyGains_ShiftOnlyThatChannel_AndKeepBlackAndWhite()
    {
        var ramp = Curves.Build(new CalibrationChoices(2.2, N, new RgbGains(0.9, 1, 1), N));

        Assert.True(ramp.Red[CalibrationCurves.MidLevel] < GammaRamp.Identity.Red[CalibrationCurves.MidLevel]);
        Assert.Equal(GammaRamp.Identity.Green, ramp.Green);
        Assert.Equal(GammaRamp.Identity.Blue, ramp.Blue);
        Assert.Equal((ushort)0, ramp.Red[0]);
        Assert.Equal((ushort)65535, ramp.Red[255]);
        // Doğrusal ışık çarpanı 0,9: kodlanmış değer 0,9^(1/2,2) ile çarpılır.
        var expected = GammaRamp.Identity.Red[CalibrationCurves.MidLevel] * Math.Pow(0.9, 1 / 2.2);
        Assert.InRange(ramp.Red[CalibrationCurves.MidLevel], expected - 1, expected + 1);
    }

    /// <summary>Beyaz kazancı (DDC yokken beyaz eşleme) bütün seviyeleri çarpar, beyazı da; tek başına <see cref="GammaRamps.Scale"/> ile aynıdır.</summary>
    [Fact]
    public void Build_WhiteGains_ScaleEveryLevel_LikeGammaRampsScale()
    {
        var white = new RgbGains(1, 0.95, 0.88);

        var ramp = Curves.Build(Neutral(white: white));

        Assert.Equal(GammaRamps.Scale(white), ramp);
        Assert.True(ramp.Blue[255] < 65535);
        Assert.Equal((ushort)65535, ramp.Red[255]);
    }

    [Theory]
    [InlineData(1.7)] [InlineData(2.9)] [InlineData(double.NaN)]
    public void Build_PanelGammaOutOfRange_Throws(double gamma) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Curves.Build(Neutral(gamma)));

    [Fact]
    public void Build_GainsOutOfRange_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Curves.Build(new(2.2, new(0.7, 1, 1), N, N)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Curves.Build(new(2.2, N, new(1, 1.3, 1), N)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Curves.Build(new(2.2, N, N, new(1, 1, double.NaN))));
        Assert.Throws<ArgumentOutOfRangeException>(() => Curves.Build(Neutral(white: new RgbGains(1, 1.05, 1)))); // beyaz sadece kısılır
        Assert.Throws<ArgumentOutOfRangeException>(() => Curves.Build(Neutral(white: new RgbGains(0.7, 1, 1))));
    }
}
