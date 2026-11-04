using System.Text;

namespace MonitorTuner.Core;

/// <summary>
/// EDID'in (monitörün kendini tanıttığı 128 baytlık taban blok) bu uygulamanın ihtiyacı olan kısmı (T102, saf mantık).
/// Checksum denetlenmez: gerçek monitörlerde yanlış checksum'lı EDID görülür ve model yine de tanınmalıdır.
/// </summary>
/// <param name="Manufacturer">Üç harfli üretici kodu (HP için <c>HPN</c>).</param>
/// <param name="SerialText">Seri no: önce metin tanımlayıcı (FF), yoksa sayısal seri (onluk), ikisi de yoksa boş.</param>
public sealed record EdidInfo(string Manufacturer, int ProductCode, string ModelName, string SerialText)
{
    private const int BaseBlockLength = 128;
    private const int FirstDescriptor = 54;
    private const int DescriptorLength = 18;
    private const byte ModelNameTag = 0xFC;
    private const byte SerialTextTag = 0xFF;

    /// <summary>Üretici ve ürün kodu birleşimi (ör. <c>HPN36A2</c>); monitör profilindeki <c>edidName</c> ile aynı biçim.</summary>
    public string EdidName => $"{Manufacturer}{ProductCode:X4}";

    /// <summary>Başlık ya da uzunluk geçersizse null. Model adı tanımlayıcısı yoksa model <see cref="EdidName"/> olur.</summary>
    public static EdidInfo? TryParse(ReadOnlySpan<byte> edid)
    {
        ReadOnlySpan<byte> header = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];
        if (edid.Length < BaseBlockLength || !edid[..8].SequenceEqual(header)) return null;

        // Üretici kimliği: büyük-endian 16 bit, 15. bit 0, sonra 5'er bitlik üç harf (1 = 'A').
        var id = (edid[8] << 8) | edid[9];
        var manufacturer = new string([Letter(id >> 10), Letter(id >> 5), Letter(id)]);
        var product = edid[10] | (edid[11] << 8); // küçük-endian

        string? model = null, serial = null;
        for (var offset = FirstDescriptor; offset + DescriptorLength <= BaseBlockLength - 1; offset += DescriptorLength)
        {
            // Metin tanımlayıcısı: 00 00 00 <etiket> 00 + 13 bayt metin. Piksel saatinin sıfır olması "tanımlayıcı" demektir.
            if (edid[offset] != 0 || edid[offset + 1] != 0 || edid[offset + 2] != 0) continue;
            var tag = edid[offset + 3];
            if (tag == ModelNameTag) model ??= Text(edid.Slice(offset + 5, 13));
            else if (tag == SerialTextTag) serial ??= Text(edid.Slice(offset + 5, 13));
        }

        var edidName = $"{manufacturer}{product:X4}";
        var numeric = BitConverter.ToUInt32([edid[12], edid[13], edid[14], edid[15]]);
        return new EdidInfo(
            manufacturer,
            product,
            string.IsNullOrEmpty(model) ? edidName : model,
            !string.IsNullOrEmpty(serial) ? serial : numeric != 0 ? numeric.ToString() : "");
    }

    private static char Letter(int fiveBits) => (char)('A' - 1 + (fiveBits & 0x1F));

    /// <summary>Metin satır sonuyla (0x0A) biter, kalanı boşlukla doldurulur; kontrol karakterleri atılır.</summary>
    private static string Text(ReadOnlySpan<byte> bytes)
    {
        var end = bytes.IndexOf((byte)0x0A);
        if (end >= 0) bytes = bytes[..end];
        var text = new StringBuilder(bytes.Length);
        foreach (var b in bytes)
            if (b >= 0x20 && b < 0x7F) text.Append((char)b);
        return text.ToString().Trim();
    }
}
