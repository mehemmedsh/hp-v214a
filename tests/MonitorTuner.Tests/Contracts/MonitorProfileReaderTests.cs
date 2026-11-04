using System.Text.Json.Nodes;
using MonitorTuner.Core;
using MonitorTuner.Core.Profiles;
using MonitorTuner.Tests.Fakes;

namespace MonitorTuner.Tests.Contracts;

/// <summary>
/// Monitör profili sözleşmesi (02-MIMARI.md) ve HP P21b G4 profili (<c>profiles/hp-p21b-g4.json</c>, T004).
/// Gerçek profil, ham EDID ve DDC dökümüyle çapraz kontrol edilir (<c>donanim/hp-p21b-g4/</c>).
/// </summary>
public class MonitorProfileReaderTests
{
    private static readonly MonitorProfileReader Reader = new();

    private static string ProfileJson() =>
        File.ReadAllText(Path.Combine(RepoRoot.Find(), "profiles", "hp-p21b-g4.json"));

    private static MonitorProfile HpProfile() => Reader.Read(ProfileJson());

    /// <summary>Gerçek profili JSON olarak düzenler; her geçersizlik testi tek bir bozulma uygular.</summary>
    private static string Mutate(Action<JsonObject> change)
    {
        var root = JsonNode.Parse(ProfileJson())!.AsObject();
        change(root);
        return root.ToJsonString();
    }

    private static JsonObject Mode(JsonObject root, int hz) =>
        root["modes"]!.AsArray().Select(n => n!.AsObject()).Single(m => (int)m["refreshHz"]! == hz);

    private static JsonObject Code(JsonObject root, string code) =>
        root["ddcCodes"]!.AsArray().Select(n => n!.AsObject()).Single(c => (string)c["code"]! == code);

    // ---- Gerçek profil ----

    /// <summary>HP P21b G4 profili: 50, 60, 72, 74, 75, 76 modları; ölçülen tavan 76, 77 reddedildi.</summary>
    [Fact]
    public void Read_HpProfile_HasModes50To76_AndMeasuredMax76()
    {
        var profile = HpProfile();

        Assert.Equal("HP P21b G4", profile.Model);
        Assert.Equal(new[] { 50, 60, 72, 74, 75, 76 }, profile.Modes.Select(m => m.RefreshHz));
        Assert.Equal(76, profile.MeasuredMaxRefreshHz);
        Assert.Equal(72, profile.SpecMaxRefreshHz);
        Assert.Contains(profile.RejectedModes, m => m.RefreshHz == 77);
        Assert.DoesNotContain(profile.Modes, m => m.RefreshHz == 77);
    }

    [Fact]
    public void Read_HpProfile_ModeStatusesAndTimingsMatchMeasurements()
    {
        var modes = HpProfile().Modes.ToDictionary(m => m.RefreshHz);

        Assert.Equal(ModeStatus.Verified, modes[60].Status);
        Assert.Equal(ModeStatus.Verified, modes[74].Status);
        Assert.Equal(ModeStatus.NvidiaPassed, modes[75].Status);
        Assert.Equal(ModeStatus.NvidiaPassed, modes[76].Status);
        Assert.Equal(ModeStatus.Untested, modes[72].Status);
        Assert.Equal(ModeStatus.Untested, modes[50].Status);

        // 00-DONANIM.md → Ünitede ölçülenler: toplamlar, yatay frekans ve pixel clock.
        AssertTiming(modes[60], 2200, 1125, 67.5, 148.5);
        AssertTiming(modes[74], 2080, 1119, 82.81, 172.24);
        AssertTiming(modes[75], 2080, 1100, 82.50, 171.60);
        AssertTiming(modes[76], 2080, 1092, 82.99, 172.62);
        Assert.Equal(new[] { 3, 5, 4 }, new[] { modes[76].Timing.VFrontPorch, modes[76].Timing.VSync, modes[76].Timing.VBackPorch });
        Assert.Equal(new[] { 3, 5, 12 }, new[] { modes[75].Timing.VFrontPorch, modes[75].Timing.VSync, modes[75].Timing.VBackPorch });
        Assert.Equal(new[] { 48, 32, 80 }, new[] { modes[76].Timing.HFrontPorch, modes[76].Timing.HSync, modes[76].Timing.HBackPorch });

        static void AssertTiming(ProfileMode m, int hTotal, int vTotal, double khz, double mhz)
        {
            Assert.Equal(hTotal, m.Timing.HTotal);
            Assert.Equal(vTotal, m.Timing.VTotal);
            Assert.Equal(khz, m.Timing.HorizontalKhz(m.RefreshHz), 2);
            Assert.Equal(mhz, m.Timing.PixelClockMhz(m.RefreshHz), 2);
        }
    }

    [Fact]
    public void Read_HpProfile_RejectedModesAreTheMeasuredOutOfRangeOnes()
    {
        var profile = HpProfile();

        Assert.All(profile.RejectedModes, m => Assert.Equal(ModeStatus.Rejected, m.Status));
        var cvt75 = Assert.Single(profile.RejectedModes, m => m.RefreshHz == 75);
        Assert.Equal(1119, cvt75.Timing.VTotal);
        var hz77 = Assert.Single(profile.RejectedModes, m => m.RefreshHz == 77);
        Assert.Equal(2000, hz77.Timing.HTotal);
        Assert.Equal(0, hz77.Timing.HBackPorch);

        // Sınır yatay frekansta (pixel clock'ta değil): reddedilen 77 Hz çalışan 76 Hz'den düşük pixel clock'ta.
        var m76 = profile.Modes.Single(m => m.RefreshHz == 76);
        Assert.True(hz77.Timing.PixelClockMhz(77) < m76.Timing.PixelClockMhz(76));
        Assert.True(hz77.Timing.HorizontalKhz(77) > m76.Timing.HorizontalKhz(76));
        Assert.Equal(83.0, profile.Measured.HighestAcceptedHorizontalKhz);
        Assert.Equal(83.9, profile.Measured.LowestRejectedHorizontalKhz);
    }

    [Fact]
    public void Read_HpProfile_OriginalSixtyHzMatchesEdidNativeTiming()
    {
        var edid = SampleEdid.HpP21bG4;
        var native = ParseDetailedTiming(edid.AsSpan(54, 18), out var pixelClockMhz);
        var sixty = HpProfile().Modes.Single(m => m.RefreshHz == SafetyLimits.OriginalRefreshHz);

        Assert.Equal(native, sixty.Timing);
        Assert.Equal(pixelClockMhz, sixty.Timing.PixelClockMhz(60), 2);
        Assert.Equal(ModeSource.Edid, sixty.Source);
    }

    [Fact]
    public void Read_HpProfile_EdidLimitsMatchRawEdidRangeDescriptor()
    {
        var edid = SampleEdid.HpP21bG4;
        var block = Enumerable.Range(0, 4).Select(i => 54 + 18 * i).First(o => edid[o] == 0 && edid[o + 3] == 0xFD);
        var limits = HpProfile().EdidLimits;

        Assert.Equal(edid[block + 5], limits.VerticalMinHz);
        Assert.Equal(edid[block + 6], limits.VerticalMaxHz);
        Assert.Equal(edid[block + 7], limits.HorizontalMinKhz);
        Assert.Equal(edid[block + 8], limits.HorizontalMaxKhz);
        Assert.Equal(edid[block + 9] * 10, limits.MaxPixelClockMhz);
    }

    [Fact]
    public void Read_HpProfile_Has50HzFromEdidAndCustomModesForTheRest()
    {
        var modes = HpProfile().Modes.ToDictionary(m => m.RefreshHz);

        Assert.Equal(ModeSource.Edid, modes[50].Source);
        Assert.Equal(2640, modes[50].Timing.HTotal);
        Assert.Equal(1125, modes[50].Timing.VTotal);
        Assert.All(new[] { 72, 74, 75, 76 }, hz => Assert.Equal(ModeSource.CustomResolution, modes[hz].Source));
    }

    [Fact]
    public void Read_HpProfile_PanelAndOsdCapabilities()
    {
        var profile = HpProfile();

        Assert.False(profile.HasPwm);
        Assert.Equal("TN", profile.Panel.Technology);
        Assert.Equal(6, profile.Panel.ColorBits);
        Assert.True(profile.Panel.UsesFrc);
        Assert.False(profile.Osd.HasOverdrive);
        Assert.True(profile.Osd.ShowsIncomingRefreshRate);
        Assert.True(profile.Osd.OverscanMustBeOff);
        Assert.Equal(OsdColorPreset.Neutral, profile.Osd.FactoryColorPreset);
        Assert.Equal(OsdColorPreset.CustomRgb, profile.Osd.RgbGainPreset);
        Assert.Equal(9, profile.Osd.ColorPresets.Count);
        Assert.Contains(OsdColorPreset.LowBlueLight, profile.Osd.ColorPresets);
    }

    [Fact]
    public void Read_HpProfile_DdcCodesMatchRecordedDump()
    {
        var profile = HpProfile();
        var dump = ParseVcpDump();

        // Profilde olan her kod dökümle uyuşur (C8 hariç: döküm R+W diyor, 00-DONANIM.md salt okunur; profil dokümanı izler).
        foreach (var info in profile.DdcCodes.Where(c => c.Code is not (0x04 or 0x05 or 0x08 or 0xC8)))
        {
            var d = dump[info.Code];
            Assert.Equal(d.Access, info.Access);
            Assert.Equal(d.Maximum, info.Maximum);
            Assert.Equal(d.Possible, info.PossibleValues);
        }

        Assert.Equal(
            new byte[] { 0x10, 0x12, 0x14, 0x16, 0x18, 0x1A, 0x60, 0x62, 0x87 }.Order(),
            profile.WritableDdcCodes.Order());
        Assert.Equal(new[] { 2, 4, 5, 8, 11, 12, 13, 14, 15 }, profile.DdcCodes.Single(c => c.Code == 0x14).PossibleValues);
    }

    [Fact]
    public void Read_HpProfile_NeverListsForbiddenCodesAsWritable_AndDocumentsFactoryResets()
    {
        var profile = HpProfile();

        Assert.All(profile.WritableDdcCodes, code => Assert.False(SafetyLimits.IsDdcWriteForbidden(code)));
        foreach (byte reset in new byte[] { 0x04, 0x05, 0x08 })
        {
            var info = profile.DdcCodes.Single(c => c.Code == reset);
            Assert.Equal(DdcAccess.WriteOnly, info.Access);
            Assert.False(info.AppMayWrite);
        }

        // 0C çalışıp çalışmadığı T301'de test edilir; 2E anlamı bilinmiyor; siyah seviyesi kullanılmıyor.
        Assert.DoesNotContain((byte)0x0C, profile.WritableDdcCodes);
        Assert.DoesNotContain((byte)0x2E, profile.WritableDdcCodes);
        Assert.DoesNotContain((byte)0x6C, profile.WritableDdcCodes);
    }

    [Fact]
    public void Read_HpProfile_ColorPresetMapCoversEveryDdcValue_OnlyNeutralConfirmed()
    {
        var profile = HpProfile();
        var possible = profile.DdcCodes.Single(c => c.Code == 0x14).PossibleValues;

        Assert.Equal(possible.Order(), profile.ColorPresetMap.Select(m => m.DdcValue).Order());
        Assert.Equal(profile.Osd.ColorPresets.Order(), profile.ColorPresetMap.Select(m => m.Preset).Order());

        var neutral = profile.ColorPresetMap.Single(m => m.DdcValue == 5);
        Assert.Equal(OsdColorPreset.Neutral, neutral.Preset);
        Assert.Equal(MappingConfidence.Confirmed, neutral.Confidence);
        Assert.Equal(OsdColorPreset.Native, profile.ColorPresetMap.Single(m => m.DdcValue == 2).Preset);
        Assert.Equal(OsdColorPreset.Warm, profile.ColorPresetMap.Single(m => m.DdcValue == 4).Preset);
        Assert.Equal(OsdColorPreset.Cool, profile.ColorPresetMap.Single(m => m.DdcValue == 8).Preset);
        Assert.Equal(OsdColorPreset.CustomRgb, profile.ColorPresetMap.Single(m => m.DdcValue == 11).Preset);
        // 12–15'in sırası OSD'den doğrulanana kadar tahmin; T307 tahmini değeri yazmamalı.
        Assert.All(profile.ColorPresetMap.Where(m => m.DdcValue != 5), m => Assert.Equal(MappingConfidence.Estimated, m.Confidence));
    }

    [Fact]
    public void Read_HpProfile_FrequencyReadoutRulesReproduceTheFirstDayReadings()
    {
        var rules = HpProfile().FrequencyReadout;

        Assert.Equal(100, rules.VerticalDivisor);
        Assert.Equal(65536, rules.HorizontalWrapModulus);
        Assert.Equal(2, rules.Samples.Count);
        foreach (var s in rules.Samples)
        {
            Assert.Equal(s.RefreshHz, s.Ae / (double)rules.VerticalDivisor);
            Assert.Equal(s.HorizontalHz, s.Ac + rules.HorizontalWrapModulus);
            Assert.InRange(s.HorizontalHz, rules.HorizontalMinHz, rules.HorizontalMaxHz);
        }

        // Örnekler gerçekten profildeki modların yatay frekansı (monitör 100 Hz'e yuvarlıyor).
        foreach (var s in rules.Samples)
        {
            var mode = HpProfile().Modes.Single(m => m.RefreshHz == s.RefreshHz);
            Assert.InRange(Math.Abs(mode.Timing.HorizontalKhz(s.RefreshHz) * 1000 - s.HorizontalHz), 0, 100);
        }
    }

    [Fact]
    public void Read_HpProfile_EveryModeIsInsideTheMeasuredLimit_AndSpecMaxIsDerivable()
    {
        var profile = HpProfile();

        Assert.All(profile.Modes, m =>
            Assert.True(m.Timing.HorizontalKhz(m.RefreshHz) <= profile.Measured.HighestAcceptedHorizontalKhz));
        Assert.All(profile.RejectedModes, m =>
            Assert.True(m.Timing.HorizontalKhz(m.RefreshHz) >= profile.Measured.LowestRejectedHorizontalKhz));
        Assert.All(profile.Modes.Where(m => m.RefreshHz <= profile.SpecMaxRefreshHz), m =>
            Assert.False(profile.IsBeyondSpec(m)));
        Assert.All(profile.Modes.Where(m => m.RefreshHz > profile.SpecMaxRefreshHz), m =>
            Assert.True(profile.IsBeyondSpec(m)));
    }

    // ---- Doğrulama kuralları ----

    /// <summary>Yazılabilir kodlarda 04, 05, 08 veya E6–FF olan profil reddedilir.</summary>
    [Theory]
    [InlineData("04")]
    [InlineData("05")]
    [InlineData("08")]
    public void Read_RejectsProfile_ListingForbiddenDdcCode(string forbidden)
    {
        var json = Mutate(root =>
        {
            Code(root, forbidden)["appMayWrite"] = true;
            Code(root, forbidden)["access"] = "readWrite";
        });

        Assert.Throws<InvalidProfileException>(() => Reader.Read(json));
    }

    [Fact]
    public void Read_RejectsProfile_ListingManufacturerCodeAsWritable()
    {
        var json = Mutate(root => root["ddcCodes"]!.AsArray().Add(new JsonObject
        {
            ["code"] = "E6", ["name"] = "Manufacturer Specific", ["access"] = "readWrite", ["maximum"] = 1,
            ["possibleValues"] = new JsonArray(0, 1), ["appMayWrite"] = true,
        }));

        Assert.Throws<InvalidProfileException>(() => Reader.Read(json));
    }

    [Fact]
    public void Read_RejectsProfile_WhereAppWritesAReadOnlyCode()
    {
        var json = Mutate(root => Code(root, "AE")["appMayWrite"] = true);
        Assert.Throws<InvalidProfileException>(() => Reader.Read(json));
    }

    [Fact]
    public void Read_RejectsProfile_WithDuplicateDdcCode()
    {
        var json = Mutate(root => root["ddcCodes"]!.AsArray().Add(Code(root, "10").DeepClone()));
        Assert.Throws<InvalidProfileException>(() => Reader.Read(json));
    }

    [Fact]
    public void Read_RejectsProfile_WithoutOriginal60HzMode()
    {
        var json = Mutate(root => root["modes"]!.AsArray().Remove(Mode(root, 60)));
        Assert.Throws<InvalidProfileException>(() => Reader.Read(json));
    }

    [Fact]
    public void Read_RejectsProfile_Where60HzIsNotVerified()
    {
        var json = Mutate(root => Mode(root, 60)["status"] = "untested");
        Assert.Throws<InvalidProfileException>(() => Reader.Read(json));
    }

    [Fact]
    public void Read_RejectsProfile_ListingRejectedModeAmongUsableModes()
    {
        var json = Mutate(root => Mode(root, 76)["status"] = "rejected");
        Assert.Throws<InvalidProfileException>(() => Reader.Read(json));
    }

    [Fact]
    public void Read_RejectsProfile_WithDuplicateRefreshRate()
    {
        var json = Mutate(root => root["modes"]!.AsArray().Add(Mode(root, 74).DeepClone()));
        Assert.Throws<InvalidProfileException>(() => Reader.Read(json));
    }

    [Fact]
    public void Read_RejectsProfile_WhereAModeExceedsTheMeasuredHorizontalLimit()
    {
        // 76 Hz'i 2080x1119 yaparsak yatay frekans 85.0 kHz olur: ölçülen sınırın (83.0) üstü.
        var json = Mutate(root => Mode(root, 76)["timing"]!["vBackPorch"] = 31);
        Assert.Throws<InvalidProfileException>(() => Reader.Read(json));
    }

    [Fact]
    public void Read_RejectsProfile_WhereARejectedModeIsBelowTheRejectedLimit()
    {
        var json = Mutate(root => root["rejectedModes"]!.AsArray()[0]!["timing"]!["vBackPorch"] = 4);
        Assert.Throws<InvalidProfileException>(() => Reader.Read(json));
    }

    [Fact]
    public void Read_RejectsProfile_WhereMeasuredMaxDoesNotMatchModes()
    {
        var json = Mutate(root => root["measured"]!["maxRefreshHz"] = 77);
        Assert.Throws<InvalidProfileException>(() => Reader.Read(json));
    }

    [Fact]
    public void Read_RejectsProfile_WhereSpecMaxDoesNotMatchEdidLimits()
    {
        var json = Mutate(root => root["specMaxRefreshHz"] = 76);
        Assert.Throws<InvalidProfileException>(() => Reader.Read(json));
    }

    [Fact]
    public void Read_RejectsProfile_WithNonPositiveTiming()
    {
        var json = Mutate(root => Mode(root, 72)["timing"]!["hSync"] = 0);
        Assert.Throws<InvalidProfileException>(() => Reader.Read(json));
    }

    [Fact]
    public void Read_RejectsProfile_WherePresetValueIsNotAPossibleDdcValue()
    {
        var json = Mutate(root => root["colorPresetMap"]!.AsArray()[0]!["ddcValue"] = 99);
        Assert.Throws<InvalidProfileException>(() => Reader.Read(json));
    }

    [Fact]
    public void Read_RejectsProfile_WhereTwoDdcValuesMapToTheSamePreset()
    {
        var json = Mutate(root =>
        {
            var map = root["colorPresetMap"]!.AsArray();
            map[1]!["preset"] = (string)map[0]!["preset"]!;
        });
        Assert.Throws<InvalidProfileException>(() => Reader.Read(json));
    }

    [Fact]
    public void Read_RejectsProfile_WithUnsupportedSchemaVersion()
    {
        var json = Mutate(root => root["schemaVersion"] = 2);
        Assert.Throws<InvalidProfileException>(() => Reader.Read(json));
    }

    [Fact]
    public void Read_RejectsProfile_WithMisspelledOrMissingFields()
    {
        var unknown = Mutate(root => root["unknownField"] = false);
        var misspelled = Mutate(root => Mode(root, 76)["refreshHZ"] = 76);
        var missing = Mutate(root => root.Remove("osd"));

        Assert.Throws<InvalidProfileException>(() => Reader.Read(unknown));
        Assert.Throws<InvalidProfileException>(() => Reader.Read(misspelled));
        Assert.Throws<InvalidProfileException>(() => Reader.Read(missing));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    public void Read_RejectsProfile_ThatIsNotJsonObject(string json) =>
        Assert.Throws<InvalidProfileException>(() => Reader.Read(json));

    [Fact]
    public void Read_RejectsProfile_WithBadDdcCodeText()
    {
        var json = Mutate(root => Code(root, "10")["code"] = "ZZ");
        Assert.Throws<InvalidProfileException>(() => Reader.Read(json));
    }

    // ---- Yardımcılar ----

    /// <summary>EDID'in 18 baytlık ayrıntılı timing tanımını (ilk tanım = native mod) <see cref="ModeTiming"/>'e çevirir.</summary>
    private static ModeTiming ParseDetailedTiming(ReadOnlySpan<byte> d, out double pixelClockMhz)
    {
        pixelClockMhz = (d[0] | d[1] << 8) / 100.0;
        int hActive = d[2] | (d[4] & 0xF0) << 4, hBlank = d[3] | (d[4] & 0x0F) << 8;
        int vActive = d[5] | (d[7] & 0xF0) << 4, vBlank = d[6] | (d[7] & 0x0F) << 8;
        int hFront = d[8] | (d[11] & 0xC0) << 2, hSync = d[9] | (d[11] & 0x30) << 4;
        int vFront = (d[10] >> 4) | (d[11] & 0x0C) << 2, vSync = (d[10] & 0x0F) | (d[11] & 0x03) << 4;
        return new ModeTiming(hActive, hFront, hSync, hBlank - hFront - hSync, vActive, vFront, vSync, vBlank - vFront - vSync);
    }

    private sealed record VcpDump(DdcAccess Access, int? Maximum, int[] Possible);

    /// <summary>ControlMyMonitor dökümünü (<c>ddc-vcp-60hz.txt</c>) kod → alanlar olarak okur; yinelenen kodun ilkini alır.</summary>
    private static Dictionary<byte, VcpDump> ParseVcpDump()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot.Find(), "donanim", "hp-p21b-g4", "ddc-vcp-60hz.txt"));
        var result = new Dictionary<byte, VcpDump>();
        foreach (var block in text.Split("==================================================").Where(b => b.Contains("VCP Code ")))
        {
            string Field(string name) => block.Split('\n').Select(l => l.TrimEnd('\r'))
                .Single(l => l.StartsWith(name, StringComparison.Ordinal)).Split(':', 2)[1].Trim();

            var code = Convert.ToByte(Field("VCP Code  "), 16);
            var access = Field("Read-Write") switch
            {
                "Read+Write" => DdcAccess.ReadWrite,
                "Read Only" => DdcAccess.ReadOnly,
                "Write Only" => DdcAccess.WriteOnly,
                _ => (DdcAccess?)null,
            };
            if (access is null) continue;
            var max = Field("Maximum Value");
            var possible = Field("Possible Values");
            result.TryAdd(code, new VcpDump(
                access.Value,
                max.Length == 0 ? null : int.Parse(max),
                possible.Length == 0 ? [] : possible.Split(',').Select(v => int.Parse(v.Trim())).ToArray()));
        }

        return result;
    }
}
