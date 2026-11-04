using MonitorTuner.Core;
using MonitorTuner.Tests.Fakes;

namespace MonitorTuner.Tests.Contracts;

/// <summary>EDID çözücü (T102, saf mantık): model adı, seri no ve üretici/ürün kodu. Örnek: kullanıcının ünitesinin EDID'i.</summary>
public class EdidInfoTests
{
    // Taban blok: 4 tanımlayıcı 54, 72, 90 ve 108. bayttan başlar; her biri 18 bayt. Örnekte 90 = model adı (FC), 108 = seri (FF).
    private const int ModelDescriptor = 90;
    private const int SerialDescriptor = 108;

    [Fact]
    public void TryParse_SampleEdid_ReadsModelSerialAndCodes()
    {
        var info = EdidInfo.TryParse(SampleEdid.HpP21bG4);

        Assert.NotNull(info);
        Assert.Equal("HP P21b G4", info.ModelName);
        Assert.Equal("1CR1481CN2", info.SerialText);
        Assert.Equal("HPN", info.Manufacturer);
        Assert.Equal(0x36A2, info.ProductCode);
        Assert.Equal("HPN36A2", info.EdidName); // profildeki edidName ile aynı
    }

    [Fact]
    public void TryParse_NoModelDescriptor_ModelFallsBackToEdidName()
    {
        var edid = SampleEdid.HpP21bG4;
        edid[ModelDescriptor + 3] = 0x10; // FC → "kullanıcı tanımlı" (model adı değil)

        Assert.Equal("HPN36A2", EdidInfo.TryParse(edid)!.ModelName);
    }

    [Fact]
    public void TryParse_NoSerialDescriptor_UsesNumericSerialOrEmpty()
    {
        var edid = SampleEdid.HpP21bG4;
        edid[SerialDescriptor + 3] = 0x10; // FF yok
        edid[12] = 0x78; edid[13] = 0x56; edid[14] = 0x34; edid[15] = 0x12;
        Assert.Equal(0x12345678.ToString(), EdidInfo.TryParse(edid)!.SerialText); // küçük-endian, onluk

        edid[12] = edid[13] = edid[14] = edid[15] = 0; // sayısal seri de yok
        Assert.Equal("", EdidInfo.TryParse(edid)!.SerialText);
    }

    [Fact]
    public void TryParse_TextIsTrimmed_AtLineFeedAndPadding()
    {
        var edid = SampleEdid.HpP21bG4;
        // "AB\n" + boşluklar; satır sonundan sonrası atılır, kontrol karakterleri sayılmaz.
        var text = "AB\n          "u8;
        text.CopyTo(edid.AsSpan(SerialDescriptor + 5, 13));

        Assert.Equal("AB", EdidInfo.TryParse(edid)!.SerialText);
    }

    [Fact]
    public void TryParse_ChecksumMismatch_StillParses()
    {
        var edid = SampleEdid.HpP21bG4;
        edid[127] ^= 0xFF; // gerçek monitörlerde checksum'ı yanlış EDID görülür; model yine de tanınmalı

        Assert.Equal("HP P21b G4", EdidInfo.TryParse(edid)!.ModelName);
    }

    [Fact]
    public void TryParse_BadHeaderOrTooShort_ReturnsNull()
    {
        var badHeader = SampleEdid.HpP21bG4;
        badHeader[1] = 0x00;

        Assert.Null(EdidInfo.TryParse(badHeader));
        Assert.Null(EdidInfo.TryParse(SampleEdid.HpP21bG4[..100]));
        Assert.Null(EdidInfo.TryParse([]));
    }
}
