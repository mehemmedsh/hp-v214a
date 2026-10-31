namespace MonitorTuner.Tests.Contracts;

/// <summary>DDC katmanı sözleşmesi (02-MIMARI.md). Kırmızı testleri görevi yazan doldurur.</summary>
public class DdcControlTests
{
    /// <summary>04/05/08, E6–FF ve profilde olmayan kodlar monitöre gitmez (Forbidden).</summary>
    [Pending("T301")]
    public void Write_ForbiddenOrUnknownCode_NeverReachesMonitor() => Assert.Fail("T301");

    /// <summary>Aynı koda kısa sürede tekrar yazma engellenir (bellek yazma sayacı artmaz).</summary>
    [Pending("T301")]
    public void Write_RepeatedSoon_IsThrottled() => Assert.Fail("T301");

    /// <summary>DDC'siz monitörde IsAvailable false, Write NoDdc.</summary>
    [Pending("T301")]
    public void NoDdc_ReportsUnavailable_AndWritesNothing() => Assert.Fail("T301");
}
