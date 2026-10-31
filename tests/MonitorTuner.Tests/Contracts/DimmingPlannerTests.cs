namespace MonitorTuner.Tests.Contracts;

/// <summary>Karartma planlayıcı sözleşmesi (02-MIMARI.md). Kırmızı testleri görevi yazan doldurur.</summary>
public class DimmingPlannerTests
{
    /// <summary>Kaydırıcı en üstte: arka ışık 100, GPU ölçeği 1.</summary>
    [Pending("T306")]
    public void Plan_Top_IsFullBacklight_AndNoGpuScaling() => Assert.Fail("T306");

    /// <summary>Kaydırıcı en altta: arka ışık 0, GPU ölçeği tam %15 (taban).</summary>
    [Pending("T306")]
    public void Plan_Bottom_NeverGoesBelow15PercentWhite() => Assert.Fail("T306");
}
