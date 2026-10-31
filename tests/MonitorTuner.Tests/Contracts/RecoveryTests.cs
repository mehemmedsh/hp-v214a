namespace MonitorTuner.Tests.Contracts;

/// <summary>Kurtarma sözleşmesi (02-MIMARI.md). Kırmızı testleri görevi yazan doldurur.</summary>
public class RecoveryTests
{
    /// <summary>GPU tablosu, GdiIcmGammaRange, dithering ve canlılık varsayılana döner.</summary>
    [Pending("T104")]
    public void ResetToDefaults_ResetsTableGammaRangeDitheringAndVibrance() => Assert.Fail("T104");
}
