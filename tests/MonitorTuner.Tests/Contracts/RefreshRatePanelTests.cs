namespace MonitorTuner.Tests.Contracts;

/// <summary>Hz paneli sözleşmesi (02-MIMARI.md). Kırmızı testleri görevi yazan doldurur.</summary>
public class RefreshRatePanelTests
{
    /// <summary>Onay gelmezse 15 sn sonra önceki moda dönülür.</summary>
    [Pending("T209")]
    public void SwitchTo_WithoutConfirm_ReturnsToPreviousModeAfter15Seconds() => Assert.Fail("T209");

    /// <summary>72 Hz üstüne ilk geçişte uyarı kabul edilmeden geçilmez.</summary>
    [Pending("T209")]
    public void SwitchTo_OutOfSpecFirstTime_NeedsWarning() => Assert.Fail("T209");

    /// <summary>Sürücünün sildiği onaylı modlar listelenir.</summary>
    [Pending("T209")]
    public void MissingRates_ListsApprovedModesDeletedByDriver() => Assert.Fail("T209");

    /// <summary>Gerçek Hz monitörden (DDC AE) okunur.</summary>
    [Pending("T209")]
    public void ReadMonitorRefreshHz_UsesDdcAe() => Assert.Fail("T209");
}
