using MonitorTuner.Color;

namespace MonitorTuner.Tests.Contracts;

/// <summary>Kelvin hesaplayıcı sözleşmesi (02-MIMARI.md, 08-GORUNTU-YONTEMLERI.md → 4).</summary>
public class KelvinCalculatorTests
{
    private static readonly KelvinCalculator Calculator = new();

    // 08-GORUNTU-YONTEMLERI.md → 4 tablosu (doğrusal ışık): Kelvin, G, B, mavi azalması %, beyaz parlaklığı %.
    public static TheoryData<int, double, double, int, int> KnownTable => new()
    {
        { 6500, 1.00, 1.00, 0, 100 },
        { 5000, 0.84, 0.63, 37, 86 },
        { 4000, 0.69, 0.38, 62, 74 },
        { 3400, 0.59, 0.24, 76, 65 },
        { 2700, 0.44, 0.10, 90, 54 },
        { 1900, 0.25, 0.00, 100, 39 },
    };

    private static RgbGains Product(KelvinSplit s) => new(s.Monitor.R * s.Gpu.R, s.Monitor.G * s.Gpu.G, s.Monitor.B * s.Gpu.B);

    private static double Luminance(RgbGains g) => 0.2126 * g.R + 0.7152 * g.G + 0.0722 * g.B;

    /// <summary>Tablo iki ondalığa yuvarlanmış; her değer ±0.01 içinde, yüzdeler ±1 puan.</summary>
    [Theory]
    [MemberData(nameof(KnownTable))]
    public void Calculate_MatchesKnownTable(int kelvin, double g, double b, int blueReductionPercent, int whitePercent)
    {
        var total = Product(Calculator.Calculate(kelvin, RgbGains.Neutral));

        Assert.Equal(1.0, total.R, 6);
        Assert.Equal(g, total.G, 0.01);
        Assert.Equal(b, total.B, 0.01);
        Assert.InRange((1 - total.B) * 100, blueReductionPercent - 1, blueReductionPercent + 1);
        Assert.InRange(Luminance(total) * 100, whitePercent - 1, whitePercent + 1);
    }

    [Fact]
    public void Calculate_6500K_IsExactlyNeutral()
    {
        var split = Calculator.Calculate(6500, new RgbGains(0.3, 0.3, 0.3));

        Assert.Equal(RgbGains.Neutral, split.Monitor);
        Assert.Equal(RgbGains.Neutral, split.Gpu);
    }

    /// <summary>DDC yoksa (taban nötr) monitör hiçbir şey almaz, tamamı GPU'da.</summary>
    [Theory]
    [InlineData(5000)]
    [InlineData(1900)]
    public void Calculate_WithoutDdc_PutsEverythingOnGpu(int kelvin)
    {
        var split = Calculator.Calculate(kelvin, RgbGains.Neutral);

        Assert.Equal(RgbGains.Neutral, split.Monitor);
        Assert.True(split.Gpu.G < 1 && split.Gpu.B < 1);
    }

    /// <summary>Önce monitör kazancı: taban aşılmıyorsa GPU nötr kalır.</summary>
    [Fact]
    public void Calculate_UsesMonitorGainFirst_ThenGpu()
    {
        var floor = new RgbGains(0.2, 0.2, 0.2);

        var mild = Calculator.Calculate(5000, floor);
        Assert.Equal(RgbGains.Neutral, mild.Gpu);
        Assert.True(mild.Monitor.B is > 0.6 and < 0.66);

        // 2700K: mavi hedefi (~0.10) monitör tabanının (0.2) altında; monitör tabana iner, kalanı GPU alır.
        var warm = Calculator.Calculate(2700, floor);
        Assert.Equal(0.2, warm.Monitor.B, 1e-9);
        Assert.Equal(0.5, warm.Gpu.B, 0.05);
        Assert.True(warm.Monitor.G > floor.G);
        Assert.Equal(1.0, warm.Gpu.G, 1e-9);

        // 1900K: mavi hedefi sıfır; monitör tabanda, GPU maviyi tamamen kapatır.
        var extreme = Calculator.Calculate(1900, floor);
        Assert.Equal(0.2, extreme.Monitor.B, 1e-9);
        Assert.Equal(0.0, extreme.Gpu.B, 1e-9);
    }

    /// <summary>Her sıcaklık ve tabanda: monitör × GPU = hedef, monitör tabanın üstünde, ikisi de (0, 1].</summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(0.5)]
    [InlineData(0.22)]
    public void Calculate_SplitAlwaysMultipliesToTarget(double floorValue)
    {
        var floor = new RgbGains(floorValue, floorValue, floorValue);
        for (var kelvin = 1900; kelvin <= 6500; kelvin += 50)
        {
            var target = Product(Calculator.Calculate(kelvin, RgbGains.Neutral));
            var split = Calculator.Calculate(kelvin, floor);
            var total = Product(split);

            Assert.Equal(target.R, total.R, 1e-9);
            Assert.Equal(target.G, total.G, 1e-9);
            Assert.Equal(target.B, total.B, 1e-9);
            foreach (var m in new[] { split.Monitor.R, split.Monitor.G, split.Monitor.B })
                Assert.InRange(m, floorValue, 1.0);
            foreach (var g in new[] { split.Gpu.R, split.Gpu.G, split.Gpu.B })
                Assert.InRange(g, 0.0, 1.0);
        }
    }

    /// <summary>Kırmızı hep 1; sıcaklık düştükçe yeşil ve mavi tek yönde azalır (kirli sarı değil, düzgün eğri).</summary>
    [Fact]
    public void Calculate_IsMonotonic_AndKeepsRedAtOne()
    {
        var previous = RgbGains.Neutral;
        for (var kelvin = 6500; kelvin >= 1900; kelvin -= 50)
        {
            var t = Product(Calculator.Calculate(kelvin, RgbGains.Neutral));
            Assert.Equal(1.0, t.R, 1e-9);
            Assert.True(t.G <= previous.G + 1e-12 && t.B <= previous.B + 1e-12, $"{kelvin}K");
            Assert.InRange(t.B, 0.0, t.G);
            previous = t;
        }
    }

    [Theory]
    [InlineData(1000, 1900)]
    [InlineData(9000, 6500)]
    public void Calculate_OutsideRange_ClampsToLimits(int requested, int clamped) =>
        Assert.Equal(Calculator.Calculate(clamped, RgbGains.Neutral), Calculator.Calculate(requested, RgbGains.Neutral));

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(1.5)]
    [InlineData(double.NaN)]
    public void Calculate_InvalidFloor_Throws(double bad) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Calculator.Calculate(5000, new RgbGains(1, 1, bad)));

    // ---- GPU katmanı tablosu ----

    [Fact]
    public void Ramps_NeutralGains_AreIdentity() =>
        Assert.Equal(MonitorTuner.Core.GammaRamp.Identity, GammaRamps.Scale(RgbGains.Neutral));

    /// <summary>Doğrusal ışık çarpanı g, kodlanmış değerde g^(1/2.2) çarpanıdır (gamma 2.2).</summary>
    [Fact]
    public void Ramps_ScaleEncodedValuesByGammaCorrectedGain()
    {
        var ramp = GammaRamps.Scale(new RgbGains(1.0, 0.5, 0.0));

        Assert.Equal(65535, ramp.Red[255]);
        Assert.Equal(65535 * Math.Pow(0.5, 1 / 2.2), ramp.Green[255], 1.0);
        Assert.Equal(0, ramp.Blue[255]);
        Assert.All(ramp.Blue, v => Assert.Equal(0, v));
        Assert.Equal(0, ramp.Green[0]);
    }

    // ---- Akşam geçişi ----

    private static readonly TimeSpan Forty = TimeSpan.FromMinutes(40);

    [Fact]
    public void Evening_StartsAt6500_EndsAtTarget_AndHoldsAfter()
    {
        Assert.Equal(6500, EveningTransition.KelvinAt(TimeSpan.FromMinutes(-5), Forty, 3400));
        Assert.Equal(6500, EveningTransition.KelvinAt(TimeSpan.Zero, Forty, 3400));
        Assert.Equal(3400, EveningTransition.KelvinAt(Forty, Forty, 3400));
        Assert.Equal(3400, EveningTransition.KelvinAt(TimeSpan.FromHours(3), Forty, 3400));
    }

    /// <summary>Adımlar en az 5 dakika arayla: 60 dakikalık geçişte en fazla 12 değişiklik (DDC yazma sayısı sınırlı kalır).</summary>
    [Fact]
    public void Evening_ChangesOnlyAtStepBoundaries_AndNeverWarmsBackUp()
    {
        var duration = TimeSpan.FromMinutes(60);
        var changes = 0;
        var last = 6500;
        for (var minute = 0; minute <= 70; minute++)
        {
            var k = EveningTransition.KelvinAt(TimeSpan.FromMinutes(minute), duration, 2700);
            Assert.True(k <= last, $"{minute}. dakika");
            if (k != last) changes++;
            last = k;
        }

        Assert.Equal(12, changes);
        Assert.Equal(2700, last);
    }
}
