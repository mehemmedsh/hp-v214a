using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MonitorTuner.Core.Profiles;

/// <summary>
/// Profil JSON'unu okur ve doğrular. Şema sıkıdır (bilinmeyen ya da eksik alan hata); sonra güvenlik ve tutarlılık
/// kuralları uygulanır. Geçersiz profil hiçbir zaman kısmen yüklenmez.
/// </summary>
public sealed class MonitorProfileReader : IMonitorProfileReader
{
    public const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false), new HexByteConverter() },
    };

    public MonitorProfile Read(string json)
    {
        MonitorProfile? profile;
        try
        {
            profile = JsonSerializer.Deserialize<MonitorProfile>(json, Options);
        }
        catch (Exception e) when (e is JsonException or ArgumentException or FormatException)
        {
            throw new InvalidProfileException($"Profil okunamadı: {e.Message}", e);
        }

        if (profile is null) throw new InvalidProfileException("Profil boş.");
        Validate(profile);
        return profile;
    }

    private static void Validate(MonitorProfile p)
    {
        Require(p.SchemaVersion == SupportedSchemaVersion, $"Desteklenmeyen şema sürümü: {p.SchemaVersion}.");
        ValidateModes(p);
        ValidateDdcCodes(p);
        ValidateColorPresets(p);
        ValidateFrequencyReadout(p.FrequencyReadout);
    }

    private static void ValidateModes(MonitorProfile p)
    {
        var limits = p.Measured;
        Require(p.Modes.Count > 0, "Profilde mod yok.");
        Require(p.Modes.Select(m => m.RefreshHz).Distinct().Count() == p.Modes.Count, "Modlarda aynı Hz iki kez var.");
        foreach (var mode in p.Modes.Concat(p.RejectedModes)) ValidateTiming(mode);

        var original = p.Modes.FirstOrDefault(m => m.RefreshHz == SafetyLimits.OriginalRefreshHz);
        Require(original is not null, "Orijinal 60 Hz mod profilde olmalı (03-GUVENLIK.md → Kural 3).");
        Require(original!.Status == ModeStatus.Verified, "Orijinal 60 Hz mod doğrulanmış (verified) olmalı.");

        foreach (var m in p.Modes)
        {
            Require(m.Status != ModeStatus.Rejected, $"{m.RefreshHz} Hz reddedilmiş ama kullanılabilir modlar arasında.");
            Require(m.Timing.HorizontalKhz(m.RefreshHz) <= limits.HighestAcceptedHorizontalKhz,
                $"{m.RefreshHz} Hz ölçülen yatay frekans sınırını aşıyor.");
        }

        foreach (var m in p.RejectedModes)
        {
            Require(m.Status == ModeStatus.Rejected, $"{m.RefreshHz} Hz reddedilen modlar arasında ama durumu rejected değil.");
            Require(m.Timing.HorizontalKhz(m.RefreshHz) >= limits.LowestRejectedHorizontalKhz,
                $"Reddedilen {m.RefreshHz} Hz modu reddedilme sınırının altında kalıyor; sınır ya da mod yanlış.");
        }

        Require(limits.HighestAcceptedHorizontalKhz < limits.LowestRejectedHorizontalKhz, "Kabul/ret yatay frekans sınırları çelişiyor.");

        var tested = p.Modes.Where(m => m.Status is ModeStatus.Verified or ModeStatus.NvidiaPassed).Select(m => m.RefreshHz);
        Require(limits.MaxRefreshHz == tested.Max(), "Ölçülen tavan Hz, doğrulanmış/NVIDIA testini geçmiş modların en yükseğine eşit olmalı.");

        var inSpec = p.Modes.Where(m => IsInsideEdidLimits(m, p.EdidLimits)).Select(m => m.RefreshHz).DefaultIfEmpty(0).Max();
        Require(p.SpecMaxRefreshHz == inSpec, $"SpecMaxRefreshHz {p.SpecMaxRefreshHz}, EDID sınırlarından türeyen {inSpec} ile uyuşmuyor.");
    }

    /// <summary>Yatay frekans ve pixel clock EDID sınırında mı (dikey 60 Hz sınırını scaler uygulamıyor).</summary>
    private static bool IsInsideEdidLimits(ProfileMode m, EdidLimits e) =>
        m.Timing.HorizontalKhz(m.RefreshHz) is var khz && khz >= e.HorizontalMinKhz && khz <= e.HorizontalMaxKhz
        && m.Timing.PixelClockMhz(m.RefreshHz) <= e.MaxPixelClockMhz;

    private static void ValidateTiming(ProfileMode m)
    {
        var t = m.Timing;
        Require(m.RefreshHz > 0, "Hz pozitif olmalı.");
        Require(t.HActive > 0 && t.VActive > 0 && t.HSync > 0 && t.VSync > 0, $"{m.RefreshHz} Hz: görünen alan ve senkron pozitif olmalı.");
        Require(t.HFrontPorch >= 0 && t.HBackPorch >= 0 && t.VFrontPorch >= 0 && t.VBackPorch >= 0, $"{m.RefreshHz} Hz: boşluk negatif olamaz.");
    }

    private static void ValidateDdcCodes(MonitorProfile p)
    {
        Require(p.DdcCodes.Select(c => c.Code).Distinct().Count() == p.DdcCodes.Count, "DDC kod tablosunda aynı kod iki kez var.");
        foreach (var c in p.DdcCodes.Where(c => c.AppMayWrite))
        {
            var hex = c.Code.ToString("X2", CultureInfo.InvariantCulture);
            Require(!SafetyLimits.IsDdcWriteForbidden(c.Code), $"DDC {hex} yazılabilir olarak işaretli ama yazılması yasak (03-GUVENLIK.md → 3).");
            Require(c.Access == DdcAccess.ReadWrite, $"DDC {hex} yazılabilir olarak işaretli ama okunur/yazılır değil.");
        }
    }

    private static void ValidateColorPresets(MonitorProfile p)
    {
        var map = p.ColorPresetMap;
        Require(map.Select(m => m.DdcValue).Distinct().Count() == map.Count, "Renk ön ayarı eşlemesinde aynı DDC değeri iki kez var.");
        Require(map.Select(m => m.Preset).Distinct().Count() == map.Count, "Renk ön ayarı eşlemesinde aynı ön ayar iki kez var.");
        Require(map.All(m => p.Osd.ColorPresets.Contains(m.Preset)), "Eşlemedeki ön ayar OSD listesinde yok.");
        if (map.Count == 0) return;

        var preset = p.DdcCodes.FirstOrDefault(c => c.Code == 0x14);
        Require(preset is not null, "Eşleme var ama DDC tablosunda 14 kodu yok.");
        Require(map.All(m => preset!.PossibleValues.Contains(m.DdcValue)), "Eşlemedeki DDC değeri 14 kodunun olası değerlerinden biri değil.");
    }

    private static void ValidateFrequencyReadout(FrequencyReadoutRules r)
    {
        Require(r.VerticalDivisor > 0 && r.HorizontalWrapModulus > 0, "AE/AC kuralları pozitif olmalı.");
        Require(r.HorizontalMinHz < r.HorizontalMaxHz, "AC aralığı çelişiyor.");
        // Aralık, taşma payından dar olmalı; yoksa düzeltilmiş değer tek olmaz.
        Require(r.HorizontalMaxHz - r.HorizontalMinHz < r.HorizontalWrapModulus, "AC aralığı taşma payından geniş: düzeltme belirsiz olur.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidProfileException(message);
    }

    /// <summary>DDC kodu JSON'da onaltılık metin: <c>"0C"</c>, <c>"1A"</c>.</summary>
    private sealed class HexByteConverter : JsonConverter<byte>
    {
        public override byte Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            if (text is { Length: 1 or 2 } && byte.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
                return value;
            throw new JsonException("DDC kodu onaltılık metin olmalı (ör. \"0C\").");
        }

        public override void Write(Utf8JsonWriter writer, byte value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString("X2", CultureInfo.InvariantCulture));
    }
}
