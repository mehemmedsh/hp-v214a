namespace MonitorTuner.Tests.Contracts;

/// <summary>Gözle kalibrasyon sözleşmesi (02-MIMARI.md). Kırmızı testleri görevi yazan doldurur.</summary>
public class CalibrationCurvesTests
{
    /// <summary>Panel gamması 2.2 ve gri dengesi nötrse eğri birim tablodur.</summary>
    [Pending("T313")]
    public void Build_NeutralChoices_GivesIdentity() => Assert.Fail("T313");

    /// <summary>Her kanalın eğrisi artan ve yumuşaktır.</summary>
    [Pending("T313")]
    public void Build_IsMonotonic_PerChannel() => Assert.Fail("T313");
}
