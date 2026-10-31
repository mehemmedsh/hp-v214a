namespace MonitorTuner.Tests.Contracts;

/// <summary>Ayar sihirbazı sözleşmesi (02-MIMARI.md). Kırmızı testleri görevi yazan doldurur.</summary>
public class SetupWizardTests
{
    /// <summary>Sihirbaz başlarken efektler kapanır, bitince geri gelir.</summary>
    [Pending("T312")]
    public void Begin_TurnsEffectsOff_FinishBringsThemBack() => Assert.Fail("T312");

    /// <summary>Bitince DDC değerleri ve kalibrasyon temel ayar olarak kaydedilir.</summary>
    [Pending("T312")]
    public void Finish_SavesBaseline() => Assert.Fail("T312");
}
