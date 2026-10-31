namespace MonitorTuner.Tests.Contracts;

/// <summary>Güvenlik katmanı sözleşmesi (02-MIMARI.md). Kırmızı testleri görevi yazan doldurur.</summary>
public class SafetyLayerTests
{
    /// <summary>Geri alma noktası yazmadan önce mevcut değeri yedekler.</summary>
    [Pending("T103")]
    public void CreateRestorePoint_BacksUpCurrentValue_BeforeWrite() => Assert.Fail("T103");

    /// <summary>Onaylanmayan değişiklik sahte saatte 15 sn sonra geri alınır; onaylanan kalır.</summary>
    [Pending("T103")]
    public void UnconfirmedChange_RollsBackAfter15Seconds() => Assert.Fail("T103");

    /// <summary>GPU tablosunu geri yüklemeyi Güvenlik kendisi yazmaz, IGpuTableOwner'dan ister.</summary>
    [Pending("T103")]
    public void RollBack_OfGpuTable_AsksColorPipelineToRestore() => Assert.Fail("T103");

    /// <summary>Fabrika ayarı ve kaldırma temizliği yedekteki değerlere döner.</summary>
    [Pending("T103")]
    public void RestoreFactoryDefaults_And_Uninstall_ReturnEverythingToBackup() => Assert.Fail("T103");
}
