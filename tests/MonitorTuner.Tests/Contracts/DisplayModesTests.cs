namespace MonitorTuner.Tests.Contracts;

/// <summary>Windows ekran katmanı sözleşmesi (02-MIMARI.md). Kırmızı testleri görevi yazan doldurur.</summary>
public class DisplayModesTests
{
    /// <summary>Listede olmayan moda geçiş başarısız; 60 Hz dahil liste değişmez.</summary>
    [Pending("T209")]
    public void SwitchMode_ToModeNotInList_Fails_AndListIsUnchanged() => Assert.Fail("T209");
}
