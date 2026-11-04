using MonitorTuner.Color;
using MonitorTuner.Core;

namespace MonitorTuner.Tests.Contracts;

/// <summary>
/// Karartma planlayıcı (T306, 02-MIMARI.md): tek kaydırıcı, önce arka ışık (DDC 100 → 0), sonra GPU ölçeği
/// (1 → %15 taban). Sınır değerler: 0, 1, geçiş noktası, taban.
/// </summary>
public class DimmingPlannerTests
{
    private static readonly DimmingPlanner Planner = new();
    private const double T = DimmingPlanner.TransitionPoint;

    /// <summary>Kaydırıcı en üstte: arka ışık 100, GPU ölçeği 1.</summary>
    [Fact]
    public void Plan_Top_IsFullBacklight_AndNoGpuScaling() =>
        Assert.Equal(new DimmingPlan(100, 1.0), Planner.Plan(1.0));

    /// <summary>Kaydırıcı en altta: arka ışık 0, GPU ölçeği tam %15 (taban).</summary>
    [Fact]
    public void Plan_Bottom_NeverGoesBelow15PercentWhite()
    {
        var plan = Planner.Plan(0.0);

        Assert.Equal(0, plan.Backlight);
        Assert.Equal(SafetyLimits.MinWhiteLevel, plan.GpuScale);
        Assert.Equal(0.15, plan.GpuScale);
    }

    [Fact]
    public void Plan_TransitionPoint_BacklightIsZero_AndGpuStillFull() =>
        Assert.Equal(new DimmingPlan(0, 1.0), Planner.Plan(T));

    [Fact]
    public void Plan_JustBelowTransition_BacklightZero_GpuStartsDimming()
    {
        var plan = Planner.Plan(T - 0.01);

        Assert.Equal(0, plan.Backlight);
        Assert.InRange(plan.GpuScale, SafetyLimits.MinWhiteLevel, 0.999);
    }

    [Fact]
    public void Plan_JustAboveTransition_GpuIsFull_BacklightBarelyLit()
    {
        var plan = Planner.Plan(T + 0.01);

        Assert.Equal(1.0, plan.GpuScale);
        Assert.InRange(plan.Backlight, 0, 3);
    }

    [Fact]
    public void Plan_MiddleOfBacklightRange_IsHalfBacklight() =>
        Assert.Equal(new DimmingPlan(50, 1.0), Planner.Plan(T + (1 - T) / 2));

    [Fact]
    public void Plan_MiddleOfDimmingRange_IsHalfwayBetweenFullAndFloor()
    {
        var plan = Planner.Plan(T / 2);

        Assert.Equal(0, plan.Backlight);
        Assert.Equal((1 + SafetyLimits.MinWhiteLevel) / 2, plan.GpuScale, 12);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-0.0001)]
    [InlineData(double.NegativeInfinity)]
    public void Plan_BelowZero_IsClampedToBottom(double slider) =>
        Assert.Equal(Planner.Plan(0), Planner.Plan(slider));

    [Theory]
    [InlineData(1.0001)]
    [InlineData(7)]
    [InlineData(double.PositiveInfinity)]
    public void Plan_AboveOne_IsClampedToTop(double slider) =>
        Assert.Equal(Planner.Plan(1), Planner.Plan(slider));

    [Fact]
    public void Plan_NaN_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Planner.Plan(double.NaN));

    /// <summary>Bütün aralıkta: taban hiç aşılmaz, değerler aralıkta, iki kademe asla aynı anda çalışmaz, sıralama düzgün.</summary>
    [Fact]
    public void Plan_Sweep_RespectsFloor_IsMonotonic_AndStagesNeverOverlap()
    {
        var previous = Planner.Plan(0);
        for (var i = 0; i <= 1000; i++)
        {
            var plan = Planner.Plan(i / 1000.0);

            Assert.InRange(plan.Backlight, 0, 100);
            Assert.InRange(plan.GpuScale, SafetyLimits.MinWhiteLevel, 1.0);
            Assert.False(plan.Backlight > 0 && plan.GpuScale < 1.0, $"{i}: iki kademe birlikte");
            Assert.True(plan.Backlight >= previous.Backlight, $"{i}: arka ışık geri gitti");
            Assert.True(plan.GpuScale >= previous.GpuScale, $"{i}: GPU ölçeği geri gitti");
            previous = plan;
        }
    }

    [Fact]
    public void Plan_Sweep_BacklightReachesEveryStepWithoutJumps()
    {
        var last = 0;
        for (var i = 0; i <= 10000; i++)
        {
            var backlight = Planner.Plan(i / 10000.0).Backlight;
            Assert.InRange(backlight - last, 0, 1);
            last = backlight;
        }

        Assert.Equal(100, last);
    }

    [Fact]
    public void SliderForBacklight_InvertsPlan_AndClamps()
    {
        for (var code = 0; code <= 100; code++)
            Assert.Equal(code, Planner.Plan(DimmingPlanner.SliderForBacklight(code)).Backlight);

        Assert.Equal(T, DimmingPlanner.SliderForBacklight(0));
        Assert.Equal(1.0, DimmingPlanner.SliderForBacklight(100));
        Assert.Equal(1.0, DimmingPlanner.SliderForBacklight(500));
        Assert.Equal(T, DimmingPlanner.SliderForBacklight(-5));
    }

    [Fact]
    public void TransitionPoint_IsInsideTheSlider() => Assert.InRange(T, 0.05, 0.5);
}
