namespace MonitorTuner.Tests.Contracts;

/// <summary>Renk boru hattı sözleşmesi (02-MIMARI.md). Kırmızı testleri görevi yazan doldurur.</summary>
public class ColorPipelineTests
{
    /// <summary>Katmanlar kalibrasyon → mavi ışık → ek karartma sırasıyla tek tabloya birleşir.</summary>
    [Pending("T302")]
    public void Apply_CombinesLayersInOrder_CalibrationBlueLightDimming() => Assert.Fail("T302");

    /// <summary>Dış program tabloyu değiştirince kullanıcının tablosu geri yüklenir ve uyarı verilir.</summary>
    [Pending("T302")]
    public void ExternalChange_IsDetected_AndUserTableRestored() => Assert.Fail("T302");

    /// <summary>Kapanışta tabloda sadece kalibrasyon kalır.</summary>
    [Pending("T302")]
    public void Shutdown_LeavesOnlyCalibration() => Assert.Fail("T302");

    /// <summary>GdiIcmGammaRange yazılmadan önce eski değer yedeklenir.</summary>
    [Pending("T302")]
    public void EnsureFullGammaRange_BacksUpOldRegistryValue() => Assert.Fail("T302");
}
