namespace MonitorTuner.Tests.Contracts;

/// <summary>Ayar deposu sözleşmesi (02-MIMARI.md). Kırmızı testleri görevi yazan doldurur.</summary>
public class SettingsStoreTests
{
    /// <summary>Bölüm yazılıp okunur; anahtar monitör + seri no + port + kart.</summary>
    [Pending("T102")]
    public void WriteThenRead_RoundTripsSection_PerMonitorKey() => Assert.Fail("T102");

    /// <summary>Geçici klasör dışında dosya oluşmaz; yedekler ayrı klasörde.</summary>
    [Pending("T102")]
    public void Write_TouchesOnlyFilesInsideItsFolder() => Assert.Fail("T102");
}
