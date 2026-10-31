namespace MonitorTuner.Tests.Contracts;

/// <summary>Monitör tanıma sözleşmesi (02-MIMARI.md). Kırmızı testleri görevi yazan doldurur.</summary>
public class MonitorIdentifierTests
{
    /// <summary>Örnek EDID'den HP P21b G4, seri no, HDMI ve kart adı okunur.</summary>
    [Pending("T102")]
    public void Identify_ReadsModelSerialConnectionAndAdapter_FromSampleEdid() => Assert.Fail("T102");

    /// <summary>VGA bağlantısı tanınır (arayüz "HDMI'a geç" uyarısı verir).</summary>
    [Pending("T102")]
    public void Identify_MarksVgaConnection_ForWarning() => Assert.Fail("T102");
}
