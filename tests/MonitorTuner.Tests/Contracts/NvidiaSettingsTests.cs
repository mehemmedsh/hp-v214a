namespace MonitorTuner.Tests.Contracts;

/// <summary>NVIDIA katmanı sözleşmesi (02-MIMARI.md). Kırmızı testleri görevi yazan doldurur.</summary>
public class NvidiaSettingsTests
{
    /// <summary>Varsayılana dönüş dithering ve canlılığı sürücü varsayılanına alır.</summary>
    [Pending("T303")]
    public void ResetToDefaults_RestoresDitheringAndVibrance() => Assert.Fail("T303");
}
