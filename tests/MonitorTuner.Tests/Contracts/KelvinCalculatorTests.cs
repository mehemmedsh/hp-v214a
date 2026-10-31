namespace MonitorTuner.Tests.Contracts;

/// <summary>Kelvin hesaplayıcı sözleşmesi (02-MIMARI.md). Kırmızı testleri görevi yazan doldurur.</summary>
public class KelvinCalculatorTests
{
    /// <summary>08-GORUNTU-YONTEMLERI.md → 4'teki tablo (6500K → 1900K) birebir.</summary>
    [Pending("T307")]
    public void Calculate_MatchesKnownTable() => Assert.Fail("T307");

    /// <summary>Önce monitör kazancı; monitör sınırına gelince kalan GPU'da; DDC yoksa tamamı GPU'da.</summary>
    [Pending("T307")]
    public void Calculate_UsesMonitorGainFirst_ThenGpu() => Assert.Fail("T307");
}
