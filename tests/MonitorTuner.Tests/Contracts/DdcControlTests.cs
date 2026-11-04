using MonitorTuner.Core;
using MonitorTuner.Core.Profiles;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;
using MonitorTuner.Services;
using MonitorTuner.Tests.Fakes;

namespace MonitorTuner.Tests.Contracts;

/// <summary>DDC katmanı sözleşmesi (02-MIMARI.md, 03-GUVENLIK.md → 3): sahte monitör, gerçek Güvenlik katmanı, sahte saat (T301).</summary>
public class DdcControlTests
{
    private const byte Brightness = 0x10, ColorPreset = 0x14, RedGain = 0x16, GreenGain = 0x18, Sharpness = 0x87;
    private static readonly DisplayId Display = FakeHardware.Display;
    private static readonly MonitorProfile Profile =
        new MonitorProfileReader().Read(File.ReadAllText(Path.Combine(RepoRoot.Find(), "profiles", "hp-p21b-g4.json")));

    // ---- İzin listesi ----

    /// <summary>04/05/08, E6–FF ve profilde yazılabilir olmayan kodlar monitöre gitmez (Forbidden).</summary>
    [Fact]
    public void Write_ForbiddenOrUnknownCode_NeverReachesMonitor()
    {
        var rig = new Rig();
        for (var c = 0; c <= 0xFF; c++)
        {
            var code = (byte)c;
            if (Profile.WritableDdcCodes.Contains(code)) continue;
            Assert.Equal(DdcWriteResult.Forbidden, rig.Ddc.Write(code, 1));
        }

        Assert.Empty(rig.Hw.Monitor.Writes);
        Assert.Equal(Profile.WritableDdcCodes.Order(), rig.Ddc.WritableCodes.Order());
        Assert.DoesNotContain(rig.Ddc.WritableCodes, SafetyLimits.IsDdcWriteForbidden);
    }

    [Fact]
    public void Write_CodeMissingFromCapabilities_IsForbidden()
    {
        var rig = new Rig(caps => caps!.Replace(" 87(", " 99("));

        Assert.DoesNotContain(Sharpness, rig.Ddc.WritableCodes);
        Assert.Equal(DdcWriteResult.Forbidden, rig.Ddc.Write(Sharpness, 5));
        Assert.Empty(rig.Hw.Monitor.Writes);
    }

    [Fact]
    public void Capabilities_WithoutVcpList_FallBackToReadableProfileCodes()
    {
        var rig = new Rig(_ => "(prot(monitor)type(lcd)model(HP P21b G4))");

        Assert.True(rig.Ddc.IsAvailable);
        Assert.Equal(Profile.WritableDdcCodes.Order(), rig.Ddc.WritableCodes.Order());
    }

    [Theory]
    [InlineData("vcp(10 12 14(05 0B) 16)", new[] { 0x10, 0x12, 0x14, 0x16 })]
    [InlineData("(prot(monitor)VCP(101216(01 02)1A)mccs_ver(2.2))", new[] { 0x10, 0x12, 0x16, 0x1A })]
    [InlineData("vcpname(10(Parlak)) vcp(0c 10)", new[] { 0x0C, 0x10 })]
    public void Capabilities_VcpCodesAreParsed(string caps, int[] expected) =>
        Assert.Equal(expected.Select(e => (byte)e), DdcCapabilities.ParseVcpCodes(caps)!.Order());

    [Theory]
    [InlineData("(prot(monitor)type(lcd))")]
    [InlineData("vcp(10 12 14(05")] // kesik dize
    public void Capabilities_MissingOrBrokenVcpList_IsNull(string caps) => Assert.Null(DdcCapabilities.ParseVcpCodes(caps));

    /// <summary>Profilin ve monitörün aralığı dışı, olası değerlerde olmayan ya da doğrulanmamış 14 eşlemesi yazılmaz.</summary>
    [Theory]
    [InlineData(0x10, 101)]
    [InlineData(0x87, 0)]
    [InlineData(0x87, 8)]
    [InlineData(0x14, 3)]  // olası değerlerde yok
    [InlineData(0x14, 13)] // Low Blue Light: eşleme tahmin (estimated)
    public void Write_ValueNotAllowed_IsForbidden(int code, int value)
    {
        var rig = new Rig();
        Assert.Equal(DdcWriteResult.Forbidden, rig.Ddc.Write((byte)code, (ushort)value));
        Assert.Empty(rig.Hw.Monitor.Writes);
    }

    // ---- Yazma ve Güvenlik ----

    [Fact]
    public void Write_GoesThroughSafetyLayer_AndCanBeRolledBack()
    {
        var rig = new Rig();

        Assert.Equal(DdcWriteResult.Written, rig.Ddc.Write(Brightness, 40));
        var write = Assert.Single(rig.Hw.Monitor.Writes);
        Assert.Equal((Brightness, (ushort)40), (write.Code, write.Value));
        Assert.Equal(new WriteRequest(WriteTarget.Ddc, Display, Brightness), write.RestorePoint.Request);

        rig.Safety.RollBack(write.RestorePoint);
        Assert.Equal(55, rig.Ddc.Read(Brightness)!.Value.Current);
    }

    [Fact]
    public void Write_ConfirmedColorPreset_IsWritten()
    {
        var rig = new Rig();
        rig.Hw.Monitor.SetMonitorValue(ColorPreset, 11); // kullanıcı OSD'den Custom RGB seçti

        Assert.Equal(DdcWriteResult.Written, rig.Ddc.Write(ColorPreset, 5)); // Neutral: confirmed
        Assert.Equal(1, rig.Hw.Monitor.WriteCount(ColorPreset));
    }

    [Fact]
    public void Write_SameValue_DoesNotReachMonitor()
    {
        var rig = new Rig();

        Assert.Equal(DdcWriteResult.Written, rig.Ddc.Write(Brightness, 55)); // zaten 55
        Assert.Equal(DdcWriteResult.Written, rig.Ddc.Write(Brightness, 40));
        rig.Hw.Clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(DdcWriteResult.Written, rig.Ddc.Write(Brightness, 40));

        Assert.Equal(1, rig.Hw.Monitor.WriteCount(Brightness));
    }

    /// <summary>Tekrar engeli önbelleğe değil monitörün şu anki değerine bakar: OSD'den değişen değer yeniden yazılabilir.</summary>
    [Fact]
    public void Write_SameValueAfterOsdChange_IsWrittenAgain()
    {
        var rig = new Rig();
        rig.Ddc.Write(Brightness, 40);
        rig.Hw.Monitor.SetMonitorValue(Brightness, 70);
        rig.Hw.Clock.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(DdcWriteResult.Written, rig.Ddc.Write(Brightness, 40));
        Assert.Equal(2, rig.Hw.Monitor.WriteCount(Brightness));
    }

    [Fact]
    public void Write_SafetyCannotBackUp_FailsWithoutWriting()
    {
        var rig = new Rig();
        Assert.True(rig.Ddc.IsAvailable);
        rig.Hw.Monitor.Responding = false; // uyku: okunamıyor, yedek alınamaz

        Assert.Equal(DdcWriteResult.Failed, rig.Ddc.Write(Brightness, 40));
        Assert.Empty(rig.Hw.Monitor.Writes);
    }

    [Fact]
    public void Write_MonitorRejects_Fails()
    {
        var rig = new Rig();
        rig.Edited.RejectWrites = true;

        Assert.Equal(DdcWriteResult.Failed, rig.Ddc.Write(Brightness, 40));
        Assert.Equal(55, rig.Ddc.Read(Brightness)!.Value.Current);
    }

    // ---- Yazma sınırı ----

    /// <summary>Aynı koda kısa sürede tekrar yazma engellenir (bellek yazma sayacı artmaz).</summary>
    [Fact]
    public void Write_RepeatedSoon_IsThrottled()
    {
        var rig = new Rig();

        Assert.Equal(DdcWriteResult.Written, rig.Ddc.Write(Brightness, 40));
        rig.Hw.Clock.Advance(DdcControl.MinWriteInterval - TimeSpan.FromMilliseconds(1));
        Assert.Equal(DdcWriteResult.Throttled, rig.Ddc.Write(Brightness, 41));
        Assert.Equal(1, rig.Hw.Monitor.WriteCount(Brightness));

        Assert.Equal(DdcWriteResult.Written, rig.Ddc.Write(GreenGain, 200)); // sınır kod başına
        rig.Hw.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(DdcWriteResult.Written, rig.Ddc.Write(Brightness, 41));
        Assert.Equal(2, rig.Hw.Monitor.WriteCount(Brightness));
    }

    [Fact]
    public void Write_TooManyPerMinute_IsThrottledUntilWindowPasses()
    {
        var rig = new Rig();
        for (var i = 0; i < DdcControl.MaxWritesPerMinute; i++)
        {
            Assert.Equal(DdcWriteResult.Written, rig.Ddc.Write(RedGain, (ushort)(200 + i)));
            rig.Hw.Clock.Advance(DdcControl.MinWriteInterval);
        }

        Assert.Equal(DdcWriteResult.Throttled, rig.Ddc.Write(RedGain, 250));
        rig.Hw.Clock.Advance(TimeSpan.FromMinutes(1) - DdcControl.MinWriteInterval * DdcControl.MaxWritesPerMinute);
        Assert.Equal(DdcWriteResult.Written, rig.Ddc.Write(RedGain, 250));
        Assert.Equal(DdcControl.MaxWritesPerMinute + 1, rig.Hw.Monitor.WriteCount(RedGain));
    }

    [Fact]
    public void Write_FailedAttempt_CountsTowardThrottle()
    {
        var rig = new Rig();
        rig.Edited.RejectWrites = true;
        rig.Ddc.Write(Brightness, 40);
        rig.Edited.RejectWrites = false;

        Assert.Equal(DdcWriteResult.Throttled, rig.Ddc.Write(Brightness, 40));
    }

    // ---- DDC'siz monitör ----

    /// <summary>DDC'siz monitörde IsAvailable false, Write NoDdc.</summary>
    [Fact]
    public void NoDdc_ReportsUnavailable_AndWritesNothing()
    {
        var rig = new Rig(monitor: FakeDdcMonitor.WithoutDdc());

        Assert.False(rig.Ddc.IsAvailable);
        Assert.Empty(rig.Ddc.WritableCodes);
        Assert.Null(rig.Ddc.Read(Brightness));
        Assert.Equal(DdcWriteResult.NoDdc, rig.Ddc.Write(Brightness, 40));
        Assert.Equal(DdcWriteResult.NoDdc, rig.Ddc.Write(0x04, 1));
    }

    [Fact]
    public void Probe_AfterMonitorWakes_FindsDdc()
    {
        var rig = new Rig();
        rig.Hw.Monitor.Responding = false;
        Assert.False(rig.Ddc.IsAvailable);

        rig.Hw.Monitor.Responding = true;
        Assert.False(rig.Ddc.IsAvailable); // sonuç saklanır; yeniden test elle
        rig.Ddc.Probe();
        Assert.True(rig.Ddc.IsAvailable);
        Assert.Contains(Brightness, rig.Ddc.WritableCodes);
    }

    // ---- AE/AC ----

    [Fact]
    public void Read_FrequencyCodes_AreRawAndCorrectedByProfile()
    {
        var rig = new Rig();
        var rules = Profile.FrequencyReadout;

        Assert.Equal(1964, rig.Ddc.Read(0xAC)!.Value.Current);
        Assert.Equal(67_500, rules.HorizontalHz(rig.Ddc.Read(0xAC)!.Value.Current));
        Assert.Equal(60.0, rules.RefreshHz(rig.Ddc.Read(0xAE)!.Value.Current));

        rig.Hw.Monitor.SetMonitorValue(0xAC, 17264);
        rig.Hw.Monitor.SetMonitorValue(0xAE, 7400);
        Assert.Equal(82_800, rules.HorizontalHz(rig.Ddc.Read(0xAC)!.Value.Current));
        Assert.Equal(74.0, rules.RefreshHz(rig.Ddc.Read(0xAE)!.Value.Current));
    }

    [Fact]
    public void FrequencyReadout_ProfileSamplesAndModes_AreCorrected()
    {
        var rules = Profile.FrequencyReadout;
        Assert.All(rules.Samples, s =>
        {
            Assert.Equal(s.HorizontalHz, rules.HorizontalHz(s.Ac));
            Assert.Equal(s.RefreshHz, rules.RefreshHz(s.Ae));
        });
        Assert.All(Profile.Modes, m =>
        {
            var horizontalHz = m.RefreshHz * m.Timing.VTotal;
            Assert.Equal(horizontalHz, rules.HorizontalHz(horizontalHz % rules.HorizontalWrapModulus));
        });
    }

    [Theory]
    [InlineData(25_000)] // ne 25 000 ne 90 536 aralıkta
    [InlineData(65_536)]
    [InlineData(-1)]
    public void FrequencyReadout_HorizontalOutOfRange_IsNull(int ac) => Assert.Null(Profile.FrequencyReadout.HorizontalHz(ac));

    [Fact]
    public void FrequencyReadout_ZeroVertical_IsNull() => Assert.Null(Profile.FrequencyReadout.RefreshHz(0));

    // ---- Kurulum ----

    /// <summary>Sahte sistem + gerçek Güvenlik katmanı + DDC katmanı. <paramref name="editCaps"/> yetenek dizesini değiştirir.</summary>
    private sealed class Rig
    {
        public Rig(Func<string?, string?>? editCaps = null, FakeDdcMonitor? monitor = null)
        {
            Hw = new FakeHardware(monitor ?? FakeDdcMonitor.HpP21bG4());
            Edited = new EditedMonitor(Hw.Monitor, editCaps ?? (c => c));
            Safety = new SafetyLayer(
                Display, [.. Profile.WritableDdcCodes],
                new SafetyHardware(Hw.Monitor, Hw.Gpu, Hw.Modes, Hw.Nvidia, Hw.GammaRange),
                () => throw new NotSupportedException("DDC testinde GPU tablosu geri yüklenmez"), new NoBackupStore(), Hw.Clock);
            Ddc = new DdcControl(Display, Edited, Safety, Profile, Hw.Clock);
        }

        public FakeHardware Hw { get; }
        public EditedMonitor Edited { get; }
        public SafetyLayer Safety { get; }
        public DdcControl Ddc { get; }
    }

    /// <summary>Sahte monitörün önünde: yetenek dizesini değiştirir, istenirse yazmayı reddeder.</summary>
    private sealed class EditedMonitor(FakeDdcMonitor inner, Func<string?, string?> editCaps) : IDdcMonitor
    {
        public bool RejectWrites { get; set; }

        public string? ReadCapabilities(DisplayId display) => inner.ReadCapabilities(display) is { } caps ? editCaps(caps) : null;

        public DdcValue? Read(DisplayId display, byte code) => inner.Read(display, code);

        public bool Write(DisplayId display, byte code, ushort value, RestorePoint restorePoint) =>
            !RejectWrites && inner.Write(display, code, value, restorePoint);
    }

    private sealed class NoBackupStore : IFactoryBackupStore
    {
        private FactoryBackup? saved;
        public FactoryBackup? Load() => saved;
        public void Save(FactoryBackup backup) => saved = backup;
    }
}
