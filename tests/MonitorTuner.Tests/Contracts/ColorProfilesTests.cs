namespace MonitorTuner.Tests.Contracts;

/// <summary>Renk profilleri sözleşmesi (02-MIMARI.md). Kırmızı testleri görevi yazan doldurur.</summary>
public class ColorProfilesTests
{
    /// <summary>Profil değerleri bir kez ayarlar; Hz değişmez.</summary>
    [Pending("T308")]
    public void Apply_SetsValuesOnce_AndNeverChangesRefreshRate() => Assert.Fail("T308");

    /// <summary>Panik: mavi ışık, ek karartma ve canlılık kapanır; kalibrasyon kalır.</summary>
    [Pending("T310")]
    public void PanicOff_TurnsOffEffects_KeepsCalibration() => Assert.Fail("T310");
}
