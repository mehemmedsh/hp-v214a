using Microsoft.Extensions.Time.Testing;
using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Core.Profiles;
using MonitorTuner.Services;

namespace MonitorTuner.Tests.Services;

/// <summary>
/// Mavi ışık filtresi (T307): Kelvin → monitör RGB kazancı (önce) + GPU katmanı (kalan); kalibrasyona fark olarak;
/// Low Blue Light kısayolu; akşam geçişi; Gece ışığı/f.lux uyarısı. Sahte DDC ve sahte boru hattıyla.
/// </summary>
public class BlueLightFilterTests
{
    private static readonly MonitorProfile Profile =
        new MonitorProfileReader().Read(File.ReadAllText(Path.Combine(RepoRoot.Find(), "profiles", "hp-p21b-g4.json")));

    private static readonly KelvinCalculator Calculator = new();
    private static readonly DateTimeOffset Evening21 = new(2026, 11, 4, 21, 0, 0, TimeSpan.Zero);

    // ---- Sahteler ----

    private sealed class FakeDdc : IDdcControl
    {
        public bool IsAvailable { get; set; } = true;
        public HashSet<byte> Writable { get; set; } = [0x10, 0x12, 0x14, 0x16, 0x18, 0x1A];
        public Dictionary<byte, ushort> Values { get; } = new() { [0x14] = 11, [0x16] = 255, [0x18] = 255, [0x1A] = 255 };
        public List<(byte Code, ushort Value, ushort Previous)> Writes { get; } = [];
        public Func<byte, DdcWriteResult>? Override { get; set; }

        IReadOnlySet<byte> IDdcControl.WritableCodes => Writable;

        public DdcValue? Read(byte code) =>
            Values.TryGetValue(code, out var v) ? new DdcValue(v, (ushort)(code == 0x14 ? 15 : 255)) : null;

        public DdcWriteResult Write(byte code, ushort value)
        {
            var result = Override?.Invoke(code) ?? DdcWriteResult.Written;
            Writes.Add((code, value, Values.GetValueOrDefault(code)));
            if (result == DdcWriteResult.Written) Values[code] = value;
            return result;
        }
    }

    private sealed class FakePipeline : IColorPipeline
    {
        public GammaRamp? Layer { get; private set; }
        public int ApplyCalls { get; private set; }
        public bool ApplyResult { get; set; } = true;
        public List<ColorLayer> LayersTouched { get; } = [];

        public void SetLayer(ColorLayer layer, GammaRamp? table)
        {
            LayersTouched.Add(layer);
            Layer = table;
        }

        public bool Apply() { ApplyCalls++; return ApplyResult; }
        public bool RestoreIfChangedExternally() => throw new NotSupportedException();
        public bool EnsureFullGammaRange() => throw new NotSupportedException();
        public void Shutdown() => throw new NotSupportedException();
        public bool ResetTable() => throw new NotSupportedException();
    }

    private sealed class FakeProbe(params ColorConflict[] found) : IColorConflictProbe
    {
        public IReadOnlyList<ColorConflict> Detect() => found;
    }

    private sealed class FakeStore : ISettingsStore
    {
        public Dictionary<string, object> Sections { get; } = [];
        public string BackupFolder => "";
        public T? Read<T>(MonitorKey monitor, string section) =>
            Sections.TryGetValue(section, out var v) && v is T t ? t : default;
        public void Write<T>(MonitorKey monitor, string section, T value) => Sections[section] = value!;
    }

    private static readonly MonitorKey Key = new("HP P21b G4", "SN1", "HDMI", "RTX 3080");

    private sealed class Rig
    {
        public FakeStore Store { get; } = new();
        public FakeDdc Ddc { get; } = new();
        public FakePipeline Pipeline { get; } = new();
        public FakeTimeProvider Clock { get; } = new(Evening21 - TimeSpan.FromHours(2));
        public BlueLightFilter Filter { get; }

        public Rig(MonitorProfile? profile = null, IColorConflictProbe? probe = null) =>
            Filter = NewFilter(profile, probe);

        /// <summary>Aynı monitör, depo ve boru hattıyla yeni örnek: uygulama yeniden açıldı.</summary>
        public BlueLightFilter NewFilter(MonitorProfile? profile = null, IColorConflictProbe? probe = null) =>
            new(profile ?? Profile, Ddc, Pipeline, Calculator, probe ?? new FakeProbe(), Clock, Store, Key);
    }

    private static RgbGains Target(int kelvin)
    {
        var s = Calculator.Calculate(kelvin, RgbGains.Neutral);
        return s.Gpu;
    }

    private static ushort Code(double linearGain, int baseCode = 255) =>
        (ushort)Math.Round(baseCode * Math.Pow(linearGain, 1 / 2.2), MidpointRounding.AwayFromZero);

    private static MonitorProfile WithLowBlueLight(MappingConfidence confidence) =>
        Profile with
        {
            ColorPresetMap = [.. Profile.ColorPresetMap.Select(m =>
                m.Preset == OsdColorPreset.LowBlueLight ? m with { Confidence = confidence } : m)],
        };

    // ---- Başlangıç ----

    [Fact]
    public void New_IsOff_AndTouchesNothing()
    {
        var rig = new Rig();

        Assert.Equal(6500, rig.Filter.Kelvin);
        Assert.False(rig.Filter.IsSuspended);
        Assert.Empty(rig.Ddc.Writes);
        Assert.Equal(0, rig.Pipeline.ApplyCalls);
    }

    [Fact]
    public void SetKelvin_6500_WhenAlreadyOff_WritesNothing()
    {
        var rig = new Rig();

        var state = rig.Filter.SetKelvin(6500);

        Assert.Equal(6500, state.Kelvin);
        Assert.Empty(rig.Ddc.Writes);
    }

    // ---- Monitör önce, GPU sonra ----

    /// <summary>5000K monitörün kazanç aralığına sığar: sadece DDC 18/1A yazılır (16 zaten tam), GPU katmanı kapalı.</summary>
    [Fact]
    public void SetKelvin_Mild_UsesOnlyMonitorGain()
    {
        var rig = new Rig();
        var target = Target(5000);

        var state = rig.Filter.SetKelvin(5000);

        Assert.True(state.Applied);
        Assert.Equal(new byte[] { 0x18, 0x1A }, rig.Ddc.Writes.Select(w => w.Code));
        Assert.Equal(Code(target.G), rig.Ddc.Values[0x18]);
        Assert.Equal(Code(target.B), rig.Ddc.Values[0x1A]);
        Assert.Equal(255, rig.Ddc.Values[0x16]);
        Assert.Null(rig.Pipeline.Layer);
        Assert.Equal(RgbGains.Neutral, state.Gpu);
    }

    /// <summary>1900K monitörün tabanını aşar: monitör tabanda (kodun %60'ı), kalanı GPU katmanında.</summary>
    [Fact]
    public void SetKelvin_Extreme_MonitorAtFloor_RestOnGpu()
    {
        var rig = new Rig();

        var state = rig.Filter.SetKelvin(1900);

        Assert.Equal(153, rig.Ddc.Values[0x1A]); // 255 × 0,6
        Assert.Equal(0.0, state.Gpu.B, 1e-9);
        Assert.NotNull(rig.Pipeline.Layer);
        Assert.All(rig.Pipeline.Layer!.Blue, v => Assert.Equal(0, v));
        Assert.Equal([ColorLayer.BlueLight], rig.Pipeline.LayersTouched.Distinct());
        // Monitör × GPU = hedef.
        var target = Target(1900);
        Assert.Equal(target.G, state.Monitor.G * state.Gpu.G, 0.01);
    }

    [Fact]
    public void SetKelvin_WithoutDdc_PutsEverythingOnGpu()
    {
        var rig = new Rig();
        rig.Ddc.IsAvailable = false;
        var target = Target(5000);

        var state = rig.Filter.SetKelvin(5000);

        Assert.Empty(rig.Ddc.Writes);
        Assert.Equal(RgbGains.Neutral, state.Monitor);
        Assert.Equal(65535 * Math.Pow(target.B, 1 / 2.2), rig.Pipeline.Layer!.Blue[255], 2.0);
        Assert.Equal(65535, rig.Pipeline.Layer.Red[255]);
    }

    /// <summary>RGB kazancı sadece Custom RGB ön ayarında etkili (profil); başka ön ayardayken monitör payı yok.</summary>
    [Fact]
    public void SetKelvin_WhenColorPresetIsNotRgbGainPreset_UsesGpuOnly()
    {
        var rig = new Rig();
        rig.Ddc.Values[0x14] = 5; // Neutral

        rig.Filter.SetKelvin(5000);

        Assert.Empty(rig.Ddc.Writes);
        Assert.NotNull(rig.Pipeline.Layer);
    }

    [Fact]
    public void SetKelvin_WhenGainCodesNotWritable_UsesGpuOnly()
    {
        var rig = new Rig();
        rig.Ddc.Writable.Remove(0x1A);

        rig.Filter.SetKelvin(4000);

        Assert.Empty(rig.Ddc.Writes);
        Assert.NotNull(rig.Pipeline.Layer);
    }

    /// <summary>Yazma sınırlandırıldıysa (Throttled) o kanalın monitör payı uygulanmamış sayılır, GPU tamamlar.</summary>
    [Fact]
    public void SetKelvin_WhenMonitorWriteFails_GpuCompensates()
    {
        var rig = new Rig { Ddc = { Override = code => code == 0x1A ? DdcWriteResult.Throttled : DdcWriteResult.Written } };
        var target = Target(4000);

        var state = rig.Filter.SetKelvin(4000);

        Assert.Equal(1.0, state.Monitor.B, 1e-9);
        Assert.Equal(target.B, state.Gpu.B, 0.01);
        Assert.Equal(255, rig.Ddc.Values[0x1A]);
        Assert.Equal(target.G, state.Monitor.G, 0.01); // yeşil monitörde
        Assert.Equal(1.0, state.Gpu.G, 1e-9);
    }

    [Fact]
    public void SetKelvin_ReportsPipelineFailure()
    {
        var rig = new Rig();
        rig.Pipeline.ApplyResult = false;

        Assert.False(rig.Filter.SetKelvin(3000).Applied);
    }

    // ---- Kalibrasyonun üstüne fark: kapatınca temel değerler ----

    [Fact]
    public void Disable_RestoresOriginalGainsAndClearsLayer()
    {
        var rig = new Rig();
        rig.Ddc.Values[0x16] = 240;
        rig.Ddc.Values[0x18] = 250;
        rig.Ddc.Values[0x1A] = 255;

        rig.Filter.SetKelvin(5000);
        rig.Filter.SetKelvin(1900); // ara değer temel sayılmaz
        var state = rig.Filter.Disable();

        Assert.Equal(6500, state.Kelvin);
        Assert.Equal((240, 250, 255), (rig.Ddc.Values[0x16], rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]));
        Assert.Null(rig.Pipeline.Layer);
        Assert.Equal(RgbGains.Neutral, state.Monitor);
    }

    /// <summary>Monitör payı baz değerin oranıdır: kalibre edilmiş (255 olmayan) baz üstüne fark uygulanır.</summary>
    [Fact]
    public void SetKelvin_AppliesAsDifferenceOnTopOfCalibratedGains()
    {
        var rig = new Rig();
        rig.Ddc.Values[0x16] = 240;
        rig.Ddc.Values[0x18] = 200;
        rig.Ddc.Values[0x1A] = 220;
        var target = Target(5000);

        rig.Filter.SetKelvin(5000);

        Assert.Equal(240, rig.Ddc.Values[0x16]);
        Assert.Equal(Code(target.G, 200), rig.Ddc.Values[0x18]);
        Assert.Equal(Code(target.B, 220), rig.Ddc.Values[0x1A]);
    }

    // ---- Kalıcı baz: çökme / kapanış ----

    /// <summary>Filtre açıkken uygulama kapandı; yeni örnek düşük kodları baz saymaz, kayıtlı bazı geri yazar.</summary>
    [Fact]
    public void NewInstance_AfterCrash_DisableRestoresOriginalGains()
    {
        var rig = new Rig();
        rig.Ddc.Values[0x16] = 240;
        rig.Ddc.Values[0x18] = 250;
        rig.Ddc.Values[0x1A] = 255;
        rig.Filter.SetKelvin(3000);
        Assert.NotEqual(255, rig.Ddc.Values[0x1A]);

        var second = rig.NewFilter();
        second.Disable();

        Assert.Equal((240, 250, 255), (rig.Ddc.Values[0x16], rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]));
    }

    /// <summary>Çökmeden sonra filtre yeniden açılırsa baz yine ilk değerdir (her seferinde ısınmaz).</summary>
    [Fact]
    public void NewInstance_AfterCrash_SetKelvinKeepsOriginalBase()
    {
        var rig = new Rig();
        rig.Filter.SetKelvin(4000);
        var first = (rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]);

        var second = rig.NewFilter();
        second.SetKelvin(4000);

        Assert.Equal(first, (rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]));
        second.Disable();
        Assert.Equal((255, 255), (rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]));
    }

    [Fact]
    public void Restore_ClearsStoredBase()
    {
        var rig = new Rig();
        rig.Filter.SetKelvin(4000);
        Assert.NotNull(rig.Store.Read<BlueLightBaseRecord>(Key, BlueLightFilter.BaseSection));

        rig.Filter.Disable();
        var second = rig.NewFilter();
        rig.Ddc.Writes.Clear();
        second.Disable();

        Assert.Empty(rig.Store.Read<BlueLightBaseRecord>(Key, BlueLightFilter.BaseSection)!.Base);
        Assert.Empty(rig.Ddc.Writes);
    }

    [Fact]
    public void Shutdown_RestoresGainsAndClearsLayer()
    {
        var rig = new Rig();
        rig.Filter.SetKelvin(1900);

        rig.Filter.Shutdown();

        Assert.Equal((255, 255, 255), (rig.Ddc.Values[0x16], rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]));
        Assert.Null(rig.Pipeline.Layer);
        Assert.Equal(1900, rig.Filter.Kelvin);
    }

    // ---- Dışarıdan değişiklik (OSD, T305): kullanıcının değeri geçerli ----

    [Fact]
    public void ExternalChange_WhileOn_IsKeptOnDisable()
    {
        var rig = new Rig();
        rig.Filter.SetKelvin(4000);
        rig.Ddc.Values[0x16] = 230; // kullanıcı OSD'den kırmızıyı değiştirdi

        rig.Filter.Disable();

        Assert.Equal(230, rig.Ddc.Values[0x16]);
        Assert.Equal((255, 255), (rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]));
    }

    /// <summary>Dış değişiklik yeni baz olur: sonraki uygulama onun üstüne fark ekler, geri yükleme onu verir.</summary>
    [Fact]
    public void ExternalChange_BecomesNewBase_ForNextApply()
    {
        var rig = new Rig();
        rig.Filter.SetKelvin(5000);
        rig.Ddc.Values[0x1A] = 200;

        rig.Filter.SetKelvin(4500);
        var target = Target(4500);
        Assert.Equal(Code(target.B, 200), rig.Ddc.Values[0x1A]);

        rig.Filter.Disable();
        Assert.Equal(200, rig.Ddc.Values[0x1A]);
    }

    [Fact]
    public void ExternalChange_AfterCrash_IsKeptToo()
    {
        var rig = new Rig();
        rig.Filter.SetKelvin(4000);
        rig.Ddc.Values[0x18] = 210;

        rig.NewFilter().Disable();

        Assert.Equal(210, rig.Ddc.Values[0x18]);
    }

    // ---- Başka renk ön ayarı: 16/18/1A o ön ayarın değerleridir, bizim yazdığımız değil ----

    /// <summary>
    /// Filtre açıkken kullanıcı OSD'de ön ayarı Custom RGB'den başka birine aldı (Neutral, 14=5): 16/18/1A farklı
    /// okunur ama kullanıcı değişikliği değildir. Kazanç yazılmaz, kayıt silinmez, GPU katmanı kapanır; Custom RGB'ye
    /// dönünce sonraki Disable kayıtlı bazı geri yazar.
    /// </summary>
    [Fact]
    public void Disable_WhileOtherColorPreset_WritesNoGainsAndKeepsStoredBase_UntilCustomRgbReturns()
    {
        var rig = new Rig();
        rig.Filter.SetKelvin(3000);
        var low = (rig.Ddc.Values[0x16], rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]);
        Assert.NotEqual((255, 255, 255), low);

        rig.Ddc.Values[0x14] = 5; // Neutral
        (rig.Ddc.Values[0x16], rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]) = (255, 255, 255);
        rig.Ddc.Writes.Clear();

        rig.Filter.Disable();

        Assert.Empty(rig.Ddc.Writes);
        Assert.Equal((255, 255, 255), (rig.Ddc.Values[0x16], rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]));
        var record = rig.Store.Read<BlueLightBaseRecord>(Key, BlueLightFilter.BaseSection)!;
        Assert.Equal(3, record.Base.Length);

        // Kullanıcı Custom RGB'ye döndü: monitör düşük kodları hatırlıyor.
        rig.Ddc.Values[0x14] = 11;
        (rig.Ddc.Values[0x16], rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]) = low;

        rig.Filter.Disable();

        Assert.Equal((255, 255, 255), (rig.Ddc.Values[0x16], rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]));
        Assert.Empty(rig.Store.Read<BlueLightBaseRecord>(Key, BlueLightFilter.BaseSection)!.Base);
    }

    /// <summary>Aynı durum, uygulama yeniden açılınca: yeni örnek Disable düşük kodları değil ilk bazı yazar.</summary>
    [Fact]
    public void Disable_AfterOtherColorPreset_NewInstanceRestoresOriginalBase()
    {
        var rig = new Rig();
        rig.Ddc.Values[0x16] = 240;
        rig.Ddc.Values[0x18] = 250;
        rig.Ddc.Values[0x1A] = 250;
        rig.Filter.SetKelvin(3000);
        var low = (rig.Ddc.Values[0x16], rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]);

        rig.Ddc.Values[0x14] = 5;
        (rig.Ddc.Values[0x16], rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]) = (255, 255, 255);
        rig.Filter.Disable();
        rig.Ddc.Values[0x14] = 11;
        (rig.Ddc.Values[0x16], rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]) = low;

        rig.NewFilter().Disable();

        Assert.Equal((240, 250, 250), (rig.Ddc.Values[0x16], rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]));
    }

    /// <summary>Başka ön ayardayken Disable GPU katmanını yine kapatır ve monitör payı etkisiz (1) bildirilir.</summary>
    [Fact]
    public void Disable_WhileOtherColorPreset_StillClearsGpuLayer()
    {
        var rig = new Rig();
        rig.Filter.SetKelvin(1900);
        Assert.NotNull(rig.Pipeline.Layer);
        rig.Ddc.Values[0x14] = 5;

        var state = rig.Filter.Disable();

        Assert.Null(rig.Pipeline.Layer);
        Assert.Equal(RgbGains.Neutral, state.Monitor);
        Assert.Equal(RgbGains.Neutral, state.Gpu);
    }

    /// <summary>
    /// Renk ön ayarı (14) okunamıyor (DDC hatası): "başka ön ayar" değildir, bilinmiyor. Kazanç yazılmaz, kayıt durur,
    /// GPU katmanı kapanır ama Applied=false döner; 14 tekrar Custom RGB olunca Disable bazı geri yazar (Applied=true).
    /// </summary>
    [Fact]
    public void Disable_WhenColorPresetUnreadable_WritesNothingKeepsBaseAndReportsNotApplied()
    {
        var rig = new Rig();
        rig.Filter.SetKelvin(3000);
        var low = (rig.Ddc.Values[0x16], rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]);
        Assert.NotEqual((255, 255, 255), low);
        Assert.NotNull(rig.Pipeline.Layer);

        rig.Ddc.Values.Remove(0x14); // okunamaz
        rig.Ddc.Writes.Clear();

        var state = rig.Filter.Disable();

        Assert.False(state.Applied);
        Assert.Empty(rig.Ddc.Writes);
        Assert.Equal(low, (rig.Ddc.Values[0x16], rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]));
        Assert.Equal(3, rig.Store.Read<BlueLightBaseRecord>(Key, BlueLightFilter.BaseSection)!.Base.Length);
        Assert.Null(rig.Pipeline.Layer);

        rig.Ddc.Values[0x14] = 11;

        state = rig.Filter.Disable();

        Assert.True(state.Applied);
        Assert.Equal((255, 255, 255), (rig.Ddc.Values[0x16], rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]));
        Assert.Empty(rig.Store.Read<BlueLightBaseRecord>(Key, BlueLightFilter.BaseSection)!.Base);
    }

    // ---- Güvenlik ----

    /// <summary>Sadece 16/18/1A yazılır (parlaklık 10, ön ayar 14 ve yasak kodlara değil); aynı değer tekrar yazılmaz.</summary>
    [Fact]
    public void Sweep_WritesOnlyGainCodes_AndNeverRewritesSameValue()
    {
        var rig = new Rig();

        for (var k = 6500; k >= 1900; k -= 100) rig.Filter.SetKelvin(k);
        rig.Filter.Suspend();
        rig.Filter.Resume();
        rig.Filter.Disable();

        Assert.All(rig.Ddc.Writes, w =>
        {
            Assert.Contains(w.Code, new byte[] { 0x16, 0x18, 0x1A });
            Assert.False(SafetyLimits.IsDdcWriteForbidden(w.Code));
            Assert.NotEqual(w.Previous, w.Value);
            Assert.True(w.Value <= 255);
        });
    }

    [Fact]
    public void SetKelvin_ClampsToRange()
    {
        var rig = new Rig();

        Assert.Equal(1900, rig.Filter.SetKelvin(500).Kelvin);
        Assert.Equal(6500, rig.Filter.SetKelvin(9000).Kelvin);
    }

    // ---- Sihirbaz / profil için geçici kapatma ----

    [Fact]
    public void Suspend_RestoresGains_ResumeAppliesAgain_KelvinKept()
    {
        var rig = new Rig();
        rig.Filter.SetKelvin(3000);
        var applied = (rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]);

        rig.Filter.Suspend();
        Assert.True(rig.Filter.IsSuspended);
        Assert.Equal(3000, rig.Filter.Kelvin);
        Assert.Equal((255, 255), (rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]));
        Assert.Null(rig.Pipeline.Layer);

        rig.Filter.Resume();
        Assert.False(rig.Filter.IsSuspended);
        Assert.Equal(applied, (rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]));
    }

    [Fact]
    public void SetKelvin_WhileSuspended_ChangesNothingUntilResume()
    {
        var rig = new Rig();
        rig.Filter.Suspend();

        var state = rig.Filter.SetKelvin(3000);

        Assert.False(state.Applied);
        Assert.Empty(rig.Ddc.Writes);
        Assert.Equal(0, rig.Pipeline.ApplyCalls);
        rig.Filter.Resume();
        Assert.NotEqual(255, rig.Ddc.Values[0x1A]);
    }

    // ---- Low Blue Light kısayolu ----

    /// <summary>Profilde eşleme "estimated": monitörde doğrulanana kadar yazılmaz.</summary>
    [Fact]
    public void LowBlueLight_Estimated_IsNotWritten()
    {
        var rig = new Rig();

        Assert.Equal(LowBlueLightResult.NotConfirmed, rig.Filter.SelectMonitorLowBlueLight());
        Assert.Empty(rig.Ddc.Writes);
    }

    /// <summary>Doğrulanmış eşlemede: önce filtre kapanır (kazançlar eski hâline), sonra 14 kodu yazılır.</summary>
    [Fact]
    public void LowBlueLight_Confirmed_DisablesFilterThenWritesPreset()
    {
        var rig = new Rig(WithLowBlueLight(MappingConfidence.Confirmed));
        rig.Filter.SetKelvin(4000);

        var result = rig.Filter.SelectMonitorLowBlueLight();

        Assert.Equal(LowBlueLightResult.Selected, result);
        Assert.Equal(6500, rig.Filter.Kelvin);
        Assert.Equal(13, rig.Ddc.Values[0x14]);
        Assert.Equal((255, 255), (rig.Ddc.Values[0x18], rig.Ddc.Values[0x1A]));
        Assert.Equal((byte)0x14, rig.Ddc.Writes[^1].Code);
        Assert.Equal(1, rig.Ddc.Writes.Count(w => w.Code == 0x14));
    }

    [Fact]
    public void LowBlueLight_WithoutDdcOrPermission_IsRefused()
    {
        var noDdc = new Rig(WithLowBlueLight(MappingConfidence.Confirmed));
        noDdc.Ddc.IsAvailable = false;
        Assert.Equal(LowBlueLightResult.NoDdc, noDdc.Filter.SelectMonitorLowBlueLight());

        var noWrite = new Rig(WithLowBlueLight(MappingConfidence.Confirmed));
        noWrite.Ddc.Writable.Remove(0x14);
        Assert.Equal(LowBlueLightResult.NotWritable, noWrite.Filter.SelectMonitorLowBlueLight());

        var failing = new Rig(WithLowBlueLight(MappingConfidence.Confirmed));
        failing.Ddc.Override = code => code == 0x14 ? DdcWriteResult.Failed : DdcWriteResult.Written;
        Assert.Equal(LowBlueLightResult.Failed, failing.Filter.SelectMonitorLowBlueLight());
    }

    [Fact]
    public void LowBlueLight_NotInProfile_IsRefused()
    {
        var profile = Profile with { ColorPresetMap = [.. Profile.ColorPresetMap.Where(m => m.Preset != OsdColorPreset.LowBlueLight)] };
        var rig = new Rig(profile);

        Assert.Equal(LowBlueLightResult.NotInProfile, rig.Filter.SelectMonitorLowBlueLight());
        Assert.Empty(rig.Ddc.Writes);
    }

    // ---- Gece ışığı / f.lux uyarısı ----

    [Fact]
    public void Conflicts_AreReportedWhenFilterTurnsOn_NotWhenOff()
    {
        var rig = new Rig(probe: new FakeProbe(ColorConflict.WindowsNightLight, ColorConflict.Flux));

        var both = new[] { ColorConflict.WindowsNightLight, ColorConflict.Flux };
        Assert.Equal(both, rig.Filter.SetKelvin(4000).Conflicts);
        Assert.Equal(both, rig.Filter.CheckConflicts());
        Assert.Empty(rig.Filter.Disable().Conflicts);
    }

    [Fact]
    public void Conflicts_EmptyWhenNothingRuns() =>
        Assert.Empty(new Rig().Filter.SetKelvin(4000).Conflicts);

    // ---- Akşam geçişi ----

    private static EveningSettings On(int minutes = 40, int target = 3400) =>
        new(Enabled: true, Start: new TimeOnly(21, 0), DurationMinutes: minutes, TargetKelvin: target);

    [Fact]
    public void Evening_DefaultsToOff_AndTickDoesNothing()
    {
        var rig = new Rig();
        Assert.False(rig.Filter.Evening.Enabled);

        rig.Clock.SetUtcNow(Evening21 + TimeSpan.FromMinutes(30));

        Assert.False(rig.Filter.Tick());
        Assert.Equal(6500, rig.Filter.Kelvin);
        Assert.Empty(rig.Ddc.Writes);
    }

    [Fact]
    public void Evening_WarmsGraduallyAfterStart_AndReachesTarget()
    {
        var rig = new Rig();
        rig.Filter.Evening = On();

        rig.Clock.SetUtcNow(Evening21 - TimeSpan.FromMinutes(1));
        Assert.False(rig.Filter.Tick());
        Assert.Equal(6500, rig.Filter.Kelvin);

        rig.Clock.SetUtcNow(Evening21 + TimeSpan.FromMinutes(20));
        Assert.True(rig.Filter.Tick());
        Assert.InRange(rig.Filter.Kelvin, 3500, 6499);

        rig.Clock.SetUtcNow(Evening21 + TimeSpan.FromMinutes(40));
        Assert.True(rig.Filter.Tick());
        Assert.Equal(3400, rig.Filter.Kelvin);
        Assert.False(rig.Filter.Tick()); // aynı adım tekrar yazılmaz
    }

    /// <summary>Dakikada bir Tick: 40 dakikalık geçişte en fazla 8 adım, her adımda en fazla 3 DDC yazması; arka ışığa dokunulmaz.</summary>
    [Fact]
    public void Evening_WritesAreFewAndColorOnly()
    {
        var rig = new Rig();
        rig.Filter.Evening = On();

        for (var minute = 0; minute <= 45; minute++)
        {
            rig.Clock.SetUtcNow(Evening21 + TimeSpan.FromMinutes(minute));
            rig.Filter.Tick();
        }

        Assert.True(rig.Ddc.Writes.Count <= 8 * 3, $"{rig.Ddc.Writes.Count} yazma");
        Assert.All(rig.Ddc.Writes, w => Assert.Contains(w.Code, new byte[] { 0x16, 0x18, 0x1A }));
    }

    /// <summary>Kullanıcı elle değiştirdiyse o akşam otomatik geçiş bir daha dokunmaz.</summary>
    [Fact]
    public void Evening_ManualChange_StopsAutomaticForThatEvening()
    {
        var rig = new Rig();
        rig.Filter.Evening = On();
        rig.Clock.SetUtcNow(Evening21 + TimeSpan.FromMinutes(10));
        rig.Filter.Tick();

        rig.Filter.Disable();
        rig.Clock.SetUtcNow(Evening21 + TimeSpan.FromMinutes(25));

        Assert.False(rig.Filter.Tick());
        Assert.Equal(6500, rig.Filter.Kelvin);

        // Ertesi akşam yeniden çalışır.
        rig.Clock.SetUtcNow(Evening21 + TimeSpan.FromDays(1) + TimeSpan.FromMinutes(40));
        Assert.True(rig.Filter.Tick());
        Assert.Equal(3400, rig.Filter.Kelvin);
    }

    /// <summary>Geçiş kullanıcının seçtiğinden daha soğuğa (yukarı) hiç çekmez.</summary>
    [Fact]
    public void Evening_NeverCoolsDownUserChoice()
    {
        var rig = new Rig();
        rig.Filter.SetKelvin(2700);
        rig.Filter.Evening = On(target: 3400);
        rig.Clock.SetUtcNow(Evening21 + TimeSpan.FromMinutes(40));

        Assert.False(rig.Filter.Tick());
        Assert.Equal(2700, rig.Filter.Kelvin);
    }

    /// <summary>Pencere dışında (geçişten 6 saat sonra) otomatik bir şey olmaz; sabaha kadar sıcak kalır, sabah kendiliğinden değişmez.</summary>
    [Fact]
    public void Evening_CatchesUpInsideHoldWindow_ButNotInTheMorning()
    {
        var late = new Rig();
        late.Filter.Evening = On();
        late.Clock.SetUtcNow(Evening21 + TimeSpan.FromHours(2)); // uygulama geç açıldı
        Assert.True(late.Filter.Tick());
        Assert.Equal(3400, late.Filter.Kelvin);

        var morning = new Rig();
        morning.Filter.Evening = On();
        morning.Clock.SetUtcNow(Evening21 + TimeSpan.FromHours(11)); // sabah 08:00
        Assert.False(morning.Filter.Tick());
        Assert.Equal(6500, morning.Filter.Kelvin);

        // Gece yarısını geçen akşam: 01:00 hâlâ dünkü akşamın penceresinde.
        var night = new Rig();
        night.Filter.Evening = On();
        night.Clock.SetUtcNow(Evening21 + TimeSpan.FromHours(4));
        Assert.True(night.Filter.Tick());
        Assert.Equal(3400, night.Filter.Kelvin);
    }

    [Fact]
    public void Evening_DoesNothingWhileSuspended()
    {
        var rig = new Rig();
        rig.Filter.Evening = On();
        rig.Filter.Suspend();
        rig.Clock.SetUtcNow(Evening21 + TimeSpan.FromMinutes(40));

        Assert.False(rig.Filter.Tick());
        Assert.Empty(rig.Ddc.Writes);
    }

    [Fact]
    public void Evening_SettingsAreNormalized()
    {
        var rig = new Rig();

        rig.Filter.Evening = On(minutes: 5, target: 800);
        Assert.Equal((30, 1900), (rig.Filter.Evening.DurationMinutes, rig.Filter.Evening.TargetKelvin));

        rig.Filter.Evening = On(minutes: 180, target: 9000);
        Assert.Equal((60, 6500), (rig.Filter.Evening.DurationMinutes, rig.Filter.Evening.TargetKelvin));
    }
}
