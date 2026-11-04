using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Services;

namespace MonitorTuner.Tests.Services;

/// <summary>
/// Parlaklık ve ek karartma servisi (T306): kaydırıcı bırakılınca tek yazma, sürüklerken donanıma dokunma yok,
/// GPU'ya sadece renk boru hattı üzerinden (Dimming katmanı), panik/askı/kapanış tek yerden. Sahte DDC ve boru hattıyla.
/// </summary>
public class BrightnessControlTests
{
    private static readonly DimmingPlanner Planner = new();

    // ---- Sahteler (ortak olay günlüğü: yazma sırası) ----

    private sealed class FakeDdc(List<string> log) : IDdcControl
    {
        public bool IsAvailable { get; set; } = true;
        public HashSet<byte> Writable { get; set; } = [0x10, 0x12];
        public ushort Maximum { get; set; } = 100;
        public ushort Current { get; set; } = 80;
        public DdcWriteResult Result { get; set; } = DdcWriteResult.Written;
        public List<(byte Code, ushort Value)> Writes { get; } = [];
        public int Reads { get; private set; }

        IReadOnlySet<byte> IDdcControl.WritableCodes => Writable;

        public DdcValue? Read(byte code)
        {
            Reads++;
            return code == 0x10 && IsAvailable ? new DdcValue(Current, Maximum) : null;
        }

        public DdcWriteResult Write(byte code, ushort value)
        {
            // Gerçek DdcControl gibi: değer zaten yerindeyse belleğe yazma yok.
            if (code == 0x10 && IsAvailable && Current == value) return DdcWriteResult.Written;

            Writes.Add((code, value));
            log.Add($"ddc:{code:X2}={value}");
            if (Result == DdcWriteResult.Written) Current = value;
            return Result;
        }
    }

    private sealed class FakePipeline(List<string> log) : IColorPipeline
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

        public bool Apply()
        {
            ApplyCalls++;
            log.Add(Layer is null ? "gpu:off" : $"gpu:{Layer.Red[255]}");
            return ApplyResult;
        }

        public bool RestoreIfChangedExternally() => throw new NotSupportedException();
        public bool EnsureFullGammaRange() => throw new NotSupportedException();
        public void Shutdown() => throw new NotSupportedException();
        public bool ResetTable() => throw new NotSupportedException();
    }

    private sealed class FixedPlanner(DimmingPlan plan) : IDimmingPlanner
    {
        public DimmingPlan Plan(double slider) => plan;
    }

    private sealed class Rig
    {
        public List<string> Log { get; } = [];
        public FakeDdc Ddc { get; }
        public FakePipeline Pipeline { get; }
        public BrightnessControl Control { get; }

        public Rig(IDimmingPlanner? planner = null)
        {
            Ddc = new FakeDdc(Log);
            Pipeline = new FakePipeline(Log);
            Control = new BrightnessControl(Ddc, Pipeline, planner ?? Planner);
        }
    }

    /// <summary>Beyaz seviyesi s (doğrusal ışık) olan katmanın en üst girişi.</summary>
    private static ushort Top(double scale) => GammaRamps.Scale(new RgbGains(scale, scale, scale)).Red[255];

    private const double Dim = 0.1;     // arka ışık 0, GPU ölçeği 0.575
    private const double Dimmer = 0.0;  // arka ışık 0, GPU ölçeği 0.15
    private const double Mid = 0.6;     // arka ışık 50
    private const double Top1 = 1.0;    // arka ışık 100

    // ---- Başlangıç ve sürükleme ----

    [Fact]
    public void New_TouchesNothing()
    {
        var rig = new Rig();

        Assert.Empty(rig.Log);
        Assert.Equal(0, rig.Ddc.Reads);
        Assert.False(rig.Control.DimmingActive);
    }

    [Fact]
    public void Preview_ReturnsPlan_AndWritesNothing()
    {
        var rig = new Rig();

        for (var i = 0; i <= 20; i++) rig.Control.Preview(i / 20.0);

        Assert.Equal(Planner.Plan(0.3), rig.Control.Preview(0.3));
        Assert.Empty(rig.Log);
        Assert.Empty(rig.Pipeline.LayersTouched);
    }

    // ---- Bırakılınca tek yazma ----

    [Fact]
    public void Commit_BacklightRange_WritesOnceToCode10_AndDoesNotTouchGpu()
    {
        var rig = new Rig();

        var state = rig.Control.Commit(Mid);

        Assert.Equal([(0x10, (ushort)50)], rig.Ddc.Writes);
        Assert.Empty(rig.Pipeline.LayersTouched);
        Assert.Equal(0, rig.Pipeline.ApplyCalls);
        Assert.True(state.Applied);
        Assert.Equal(DdcWriteResult.Written, state.BacklightWrite);
        Assert.Equal(new DimmingPlan(50, 1.0), state.Plan);
        Assert.False(rig.Control.DimmingActive);
    }

    [Fact]
    public void Commit_Top_WritesBacklight100()
    {
        var rig = new Rig();

        rig.Control.Commit(Top1);

        Assert.Equal([(0x10, (ushort)100)], rig.Ddc.Writes);
    }

    [Fact]
    public void Commit_Bottom_BacklightZero_AndGpuAtExactly15PercentFloor()
    {
        var rig = new Rig();

        var state = rig.Control.Commit(Dimmer);

        Assert.Equal([(0x10, (ushort)0)], rig.Ddc.Writes);
        Assert.Equal(1, rig.Pipeline.ApplyCalls);
        Assert.NotNull(rig.Pipeline.Layer);
        Assert.Equal(Top(SafetyLimits.MinWhiteLevel), rig.Pipeline.Layer.Red[255]);
        Assert.True(rig.Control.DimmingActive);
        Assert.True(state.Applied);
    }

    [Fact]
    public void Commit_Bottom_LeavesBlackAlone_AndDimsAllChannelsEqually()
    {
        var rig = new Rig();

        rig.Control.Commit(Dimmer);

        var layer = rig.Pipeline.Layer!;
        Assert.Equal(0, layer.Red[0]);
        Assert.Equal(layer.Red, layer.Green);
        Assert.Equal(layer.Red, layer.Blue);
        Assert.True(layer.Red[255] < ushort.MaxValue);
    }

    [Fact]
    public void Commit_InDimmingRange_WhiteScaleFollowsPlan()
    {
        var rig = new Rig();

        rig.Control.Commit(Dim);

        Assert.Equal(Top(0.575), rig.Pipeline.Layer!.Red[255]);
    }

    [Fact]
    public void Commit_OnlyTouchesDimmingLayer()
    {
        var rig = new Rig();

        rig.Control.Commit(Dim);
        rig.Control.Commit(Mid);
        rig.Control.Commit(Dimmer);
        rig.Control.DisableDimming();

        Assert.All(rig.Pipeline.LayersTouched, l => Assert.Equal(ColorLayer.Dimming, l));
    }

    [Fact]
    public void Commit_OnlyEverWritesCode10()
    {
        var rig = new Rig();

        foreach (var s in new[] { Top1, Mid, Dim, Dimmer, 0.3, 0.9 }) rig.Control.Commit(s);

        Assert.All(rig.Ddc.Writes, w => Assert.Equal(0x10, w.Code));
    }

    [Fact]
    public void Commit_SameSliderTwice_WritesOnce()
    {
        var rig = new Rig();

        rig.Control.Commit(Dimmer);
        rig.Control.Commit(Dimmer);

        Assert.Single(rig.Ddc.Writes);
        Assert.Equal(1, rig.Pipeline.ApplyCalls);
    }

    [Fact]
    public void Commit_AfterExternalBacklightChange_WritesAgain()
    {
        var rig = new Rig();
        rig.Control.Commit(Mid);
        rig.Ddc.Current = 30; // OSD / DdcPanel / sihirbaz değiştirdi

        var state = rig.Control.Commit(Mid);

        Assert.Equal(50, rig.Ddc.Current);
        Assert.Equal(2, rig.Ddc.Writes.Count);
        Assert.True(state.Applied);
    }

    [Fact]
    public void Commit_WithinDimmingRange_DoesNotRewriteBacklight()
    {
        var rig = new Rig();

        rig.Control.Commit(Dim);
        rig.Control.Commit(0.05);
        rig.Control.Commit(Dimmer);

        Assert.Single(rig.Ddc.Writes);
        Assert.Equal(3, rig.Pipeline.ApplyCalls);
    }

    [Fact]
    public void Commit_WithinBacklightRange_DoesNotTouchGpu()
    {
        var rig = new Rig();

        rig.Control.Commit(0.5);
        rig.Control.Commit(0.8);

        Assert.Equal(2, rig.Ddc.Writes.Count);
        Assert.Equal(0, rig.Pipeline.ApplyCalls);
    }

    [Fact]
    public void Commit_BacklightValueScalesToMonitorMaximum()
    {
        var rig = new Rig();
        rig.Ddc.Maximum = 255;

        rig.Control.Commit(Top1);
        rig.Control.Commit(Mid);

        Assert.Equal([(0x10, (ushort)255), (0x10, (ushort)128)], rig.Ddc.Writes);
    }

    [Fact]
    public void Commit_MaximumUnreadable_FallsBackTo100()
    {
        var rig = new Rig();
        rig.Ddc.Maximum = 0;

        rig.Control.Commit(Mid);

        Assert.Equal([(0x10, (ushort)50)], rig.Ddc.Writes);
    }

    // ---- Yazma sırası: ara durum hiçbir zaman başlangıç ve hedeften parlak olmaz ----

    [Fact]
    public void Commit_MovingDown_DimsGpuFirst_ThenLowersBacklight()
    {
        var rig = new Rig();
        rig.Control.Commit(Top1);
        rig.Log.Clear();

        rig.Control.Commit(Dimmer);

        Assert.Equal([$"gpu:{Top(0.15)}", "ddc:10=0"], rig.Log);
    }

    [Fact]
    public void Commit_MovingUp_RaisesBacklightFirst_ThenReleasesGpu()
    {
        var rig = new Rig();
        rig.Control.Commit(Dimmer);
        rig.Log.Clear();

        rig.Control.Commit(Top1);

        Assert.Equal(["ddc:10=100", "gpu:off"], rig.Log);
        Assert.False(rig.Control.DimmingActive);
    }

    [Fact]
    public void Commit_BackToBacklightRange_RemovesLayerWithNullTable()
    {
        var rig = new Rig();
        rig.Control.Commit(Dim);

        rig.Control.Commit(0.5);

        Assert.Null(rig.Pipeline.Layer);
    }

    // ---- DDC hataları ----

    [Fact]
    public void Commit_Throttled_ReportsNotApplied_AndRetriesTheWriteNextTime()
    {
        var rig = new Rig();
        rig.Ddc.Result = DdcWriteResult.Throttled;

        var first = rig.Control.Commit(Mid);
        rig.Ddc.Result = DdcWriteResult.Written;
        var second = rig.Control.Commit(Mid);

        Assert.False(first.Applied);
        Assert.Equal(DdcWriteResult.Throttled, first.BacklightWrite);
        Assert.True(second.Applied);
        Assert.Equal(2, rig.Ddc.Writes.Count);
    }

    [Fact]
    public void Commit_MovingUp_WhenBacklightWriteFails_KeepsGpuDimmed()
    {
        var rig = new Rig();
        rig.Control.Commit(Dimmer);
        rig.Ddc.Result = DdcWriteResult.Failed;
        rig.Log.Clear();

        var state = rig.Control.Commit(Top1);

        Assert.False(state.Applied);
        Assert.True(rig.Control.DimmingActive);
        Assert.DoesNotContain("gpu:off", rig.Log);
    }

    [Fact]
    public void Commit_MovingDown_WhenBacklightWriteFails_StillDimsGpu_AndRetriesBacklightLater()
    {
        var rig = new Rig();
        rig.Control.Commit(Top1);
        rig.Ddc.Result = DdcWriteResult.Throttled;

        var failed = rig.Control.Commit(Dimmer);
        rig.Ddc.Result = DdcWriteResult.Written;
        var retried = rig.Control.Commit(Dimmer);

        Assert.False(failed.Applied);
        Assert.True(retried.Applied);
        Assert.Equal((0x10, (ushort)0), rig.Ddc.Writes[^1]);
        Assert.Equal(3, rig.Ddc.Writes.Count);
    }

    [Fact]
    public void Commit_NoDdc_StillAppliesGpuDimming_ButReportsBacklightUnavailable()
    {
        var rig = new Rig();
        rig.Ddc.IsAvailable = false;

        var state = rig.Control.Commit(Dimmer);

        Assert.Empty(rig.Ddc.Writes);
        Assert.Equal(DdcWriteResult.NoDdc, state.BacklightWrite);
        Assert.False(state.Applied);
        Assert.True(rig.Control.DimmingActive);
    }

    [Fact]
    public void Commit_Code10NotWritable_WritesNothingToDdc()
    {
        var rig = new Rig();
        rig.Ddc.Writable = [0x12];

        var state = rig.Control.Commit(Mid);

        Assert.Empty(rig.Ddc.Writes);
        Assert.Equal(DdcWriteResult.Forbidden, state.BacklightWrite);
        Assert.False(state.Applied);
    }

    [Fact]
    public void Commit_PipelineApplyFails_ReportsNotApplied_AndRetriesNextTime()
    {
        var rig = new Rig();
        rig.Pipeline.ApplyResult = false;

        var failed = rig.Control.Commit(Dimmer);
        rig.Pipeline.ApplyResult = true;
        var retried = rig.Control.Commit(Dimmer);

        Assert.False(failed.Applied);
        Assert.True(retried.Applied);
        Assert.Equal(2, rig.Pipeline.ApplyCalls);
    }

    [Fact]
    public void Commit_ApplyFails_DoesNotLeaveRejectedLayerInPipeline()
    {
        var rig = new Rig();
        rig.Control.Commit(Dim);                 // 0.575 uygulandı
        rig.Pipeline.ApplyResult = false;
        var rejected = rig.Control.Commit(Dimmer); // 0.15 reddedildi
        rig.Pipeline.ApplyResult = true;

        rig.Control.Commit(Dim);                 // önceki ölçeğe dönüş: "aynı ölçek", yazma yok

        Assert.False(rejected.Applied);
        Assert.Equal(Top(0.575), rig.Pipeline.Layer!.Red[255]);
        Assert.True(rig.Control.DimmingActive);
    }

    [Fact]
    public void Commit_ApplyFailsWhenLayerWasOff_RestoresNullLayer()
    {
        var rig = new Rig();
        rig.Pipeline.ApplyResult = false;

        rig.Control.Commit(Dimmer);

        Assert.Null(rig.Pipeline.Layer);
        Assert.False(rig.Control.DimmingActive);
    }

    // ---- Taban savunması ----

    [Fact]
    public void Commit_PlannerBelowFloor_IsClampedTo15PercentBeforeReachingPipeline()
    {
        var rig = new Rig(new FixedPlanner(new DimmingPlan(0, 0.01)));

        rig.Control.Commit(0);

        Assert.Equal(Top(SafetyLimits.MinWhiteLevel), rig.Pipeline.Layer!.Red[255]);
    }

    [Fact]
    public void Commit_PlannerBacklightOutOfRange_IsClampedTo0Through100()
    {
        var rig = new Rig(new FixedPlanner(new DimmingPlan(250, 1.0)));

        rig.Control.Commit(1);

        Assert.Equal((0x10, (ushort)100), rig.Ddc.Writes.Single());
    }

    // ---- Panik, askı, kapanış ----

    [Fact]
    public void DisableDimming_RemovesLayer_AndLeavesBacklightAlone()
    {
        var rig = new Rig();
        rig.Control.Commit(Dimmer);
        rig.Ddc.Writes.Clear();

        var applied = rig.Control.DisableDimming();

        Assert.True(applied);
        Assert.Null(rig.Pipeline.Layer);
        Assert.False(rig.Control.DimmingActive);
        Assert.Empty(rig.Ddc.Writes);
    }

    [Fact]
    public void DisableDimming_WhenNothingActive_TouchesNothing()
    {
        var rig = new Rig();

        Assert.True(rig.Control.DisableDimming());
        Assert.True(rig.Control.DisableDimming());

        Assert.Empty(rig.Log);
    }

    [Fact]
    public void DisableDimming_StaysOffUntilUserMovesSliderAgain()
    {
        var rig = new Rig();
        rig.Control.Commit(Dimmer);
        rig.Control.DisableDimming();

        Assert.True(rig.Control.Resume());          // askıdan dönüş paniği geri almaz
        Assert.False(rig.Control.DimmingActive);

        rig.Control.Commit(Dim);                     // kullanıcı kaydırıcıyı oynattı

        Assert.True(rig.Control.DimmingActive);
        Assert.Equal(Top(0.575), rig.Pipeline.Layer!.Red[255]);
    }

    [Fact]
    public void DisableDimming_PipelineFails_ReturnsFalse()
    {
        var rig = new Rig();
        rig.Control.Commit(Dimmer);
        rig.Pipeline.ApplyResult = false;

        Assert.False(rig.Control.DisableDimming());
        Assert.Equal(Top(0.15), rig.Pipeline.Layer!.Red[255]); // reddedilen "kapalı" değil, önceki katman kalır
        Assert.True(rig.Control.DimmingActive);
    }

    [Fact]
    public void Suspend_RemovesLayer_WithoutTouchingBacklight_AndResumeRestoresIt()
    {
        var rig = new Rig();
        rig.Control.Commit(Dim);
        rig.Ddc.Writes.Clear();

        Assert.True(rig.Control.Suspend());
        Assert.Null(rig.Pipeline.Layer);
        Assert.False(rig.Control.DimmingActive);

        Assert.True(rig.Control.Resume());
        Assert.Equal(Top(0.575), rig.Pipeline.Layer!.Red[255]);
        Assert.True(rig.Control.DimmingActive);
        Assert.Empty(rig.Ddc.Writes);
    }

    [Fact]
    public void Commit_WhileSuspended_WritesBacklight_ButKeepsGpuLayerOff_UntilResume()
    {
        var rig = new Rig();
        rig.Control.Suspend();

        rig.Control.Commit(Dimmer);

        Assert.Single(rig.Ddc.Writes);
        Assert.Equal(0, rig.Pipeline.ApplyCalls);
        Assert.False(rig.Control.DimmingActive);

        rig.Control.Resume();

        Assert.Equal(Top(0.15), rig.Pipeline.Layer!.Red[255]);
    }

    /// <summary>Sihirbaz arka ışığı değiştirdiyse (T312): eski kaydırıcı unutulur, yeni yerin katmanı kurulur, arka ışığa yazılmaz.</summary>
    [Fact]
    public void ResumeAt_ReplacesSavedSlider_WithoutWritingBacklight()
    {
        var rig = new Rig();
        rig.Control.Commit(Dim);
        rig.Control.Suspend();
        rig.Ddc.Writes.Clear();

        Assert.True(rig.Control.ResumeAt(Mid));
        Assert.Null(rig.Pipeline.Layer);
        Assert.False(rig.Control.DimmingActive);
        Assert.Empty(rig.Ddc.Writes);

        rig.Control.Suspend();
        Assert.True(rig.Control.ResumeAt(Dimmer)); // ek karartma bölümündeki bir yer katmanı açar
        Assert.Equal(Top(0.15), rig.Pipeline.Layer!.Red[255]);
    }

    [Fact]
    public void Resume_BeforeAnySliderUse_TouchesNothing()
    {
        var rig = new Rig();

        Assert.True(rig.Control.Resume());

        Assert.Empty(rig.Log);
    }

    [Fact]
    public void Shutdown_RemovesLayer_AndWritesNoBacklight()
    {
        var rig = new Rig();
        rig.Control.Commit(Dimmer);
        rig.Ddc.Writes.Clear();

        Assert.True(rig.Control.Shutdown());

        Assert.Null(rig.Pipeline.Layer);
        Assert.Empty(rig.Ddc.Writes);
    }

    // ---- Okuma ----

    [Fact]
    public void ReadSlider_MapsCurrentBacklightIntoBacklightRange_WithoutWriting()
    {
        var rig = new Rig();
        rig.Ddc.Current = 50;

        var slider = rig.Control.ReadSlider();

        Assert.Equal(Mid, slider!.Value, 12);
        Assert.Empty(rig.Log);
    }

    [Fact]
    public void ReadSlider_ScalesFromMonitorMaximum()
    {
        var rig = new Rig();
        rig.Ddc.Maximum = 255;
        rig.Ddc.Current = 255;

        Assert.Equal(1.0, rig.Control.ReadSlider()!.Value, 12);
    }

    [Fact]
    public void ReadSlider_DdcUnavailable_ReturnsNull()
    {
        var rig = new Rig();
        rig.Ddc.IsAvailable = false;

        Assert.Null(rig.Control.ReadSlider());
    }

    [Fact]
    public void ReadSlider_AfterDimming_ReportsBottomOfDimmingRange()
    {
        var rig = new Rig();
        rig.Control.Commit(Dimmer);

        Assert.Equal(Dimmer, rig.Control.ReadSlider()!.Value);
    }
}
