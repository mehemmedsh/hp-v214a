using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Services;

namespace MonitorTuner.Tests.Services;

/// <summary>
/// Ton eğrisi servisi (T320): katmanı yalnız renk boru hattı üzerinden kurar; panik kapatır, sihirbaz askıya alır, Windows'un
/// reddettiği tablo katmanda kalmaz. Sahte boru hattıyla; gerçek tabloyla uçtan uca <c>AppRuntimeTests</c>'te.
/// </summary>
public class ToneControlTests
{
    private static readonly ToneCurves Curves = new();

    private sealed class FakePipeline : IColorPipeline
    {
        public GammaRamp? Tone { get; private set; }
        public List<string> Log { get; } = [];
        public bool ApplyResult { get; set; } = true;

        public void SetLayer(ColorLayer layer, GammaRamp? table)
        {
            Assert.Equal(ColorLayer.Tone, layer); // ton servisi başka katmana dokunmaz
            Tone = table;
            Log.Add(table is null ? "tone:off" : "tone:on");
        }

        public bool Apply()
        {
            Log.Add("apply");
            return ApplyResult;
        }

        public bool RestoreIfChangedExternally() => throw new NotSupportedException();
        public bool EnsureFullGammaRange() => throw new NotSupportedException();
        public void Shutdown() => throw new NotSupportedException();
        public void ResetTable() => throw new NotSupportedException();
    }

    private static (ToneControl Tone, FakePipeline Pipeline) Create()
    {
        var pipeline = new FakePipeline();
        return (new ToneControl(pipeline, Curves), pipeline);
    }

    [Fact]
    public void StartsStandard_WithoutTouchingTheTable()
    {
        var (tone, pipeline) = Create();
        Assert.Equal(ToneStyle.Standard, tone.Style);
        Assert.False(tone.IsSuspended);
        Assert.Empty(pipeline.Log);
    }

    [Fact]
    public void Set_PutsTheCurveOnTheToneLayer_AndWritesOnce()
    {
        var (tone, pipeline) = Create();

        Assert.True(tone.Set(ToneStyle.Cinema));

        Assert.Equal(Curves.Build(ToneStyle.Cinema), pipeline.Tone);
        Assert.Equal(["tone:on", "apply"], pipeline.Log);
        Assert.Equal(ToneStyle.Cinema, tone.Style);
    }

    [Fact]
    public void Set_Standard_RemovesTheLayer()
    {
        var (tone, pipeline) = Create();
        tone.Set(ToneStyle.Contrast);

        Assert.True(tone.Set(ToneStyle.Standard));

        Assert.Null(pipeline.Tone);
        Assert.Equal(ToneStyle.Standard, tone.Style);
    }

    /// <summary>Windows tabloyu reddederse önceki ton katmanda ve seçimde kalır; reddedilen tablo sonraki yazmalara karışmaz.</summary>
    [Fact]
    public void Set_RejectedTable_KeepsThePreviousTone()
    {
        var (tone, pipeline) = Create();
        tone.Set(ToneStyle.Cinema);
        pipeline.ApplyResult = false;

        Assert.False(tone.Set(ToneStyle.Contrast));

        Assert.Equal(Curves.Build(ToneStyle.Cinema), pipeline.Tone);
        Assert.Equal(ToneStyle.Cinema, tone.Style);
    }

    /// <summary>Sihirbaz sırasında katman kalkar; o arada gelen seçim yazılmadan saklanır, sihirbaz bitince gelir.</summary>
    [Fact]
    public void Suspend_TakesTheLayerOff_StoresNewChoices_ResumeBringsThemBack()
    {
        var (tone, pipeline) = Create();
        tone.Set(ToneStyle.Cinema);

        Assert.True(tone.Suspend());
        Assert.True(tone.IsSuspended);
        Assert.Null(pipeline.Tone);

        pipeline.Log.Clear();
        Assert.False(tone.Set(ToneStyle.Contrast)); // askıda: ekranda değil
        Assert.Empty(pipeline.Log);
        Assert.Equal(ToneStyle.Contrast, tone.Style);

        Assert.True(tone.Resume());
        Assert.False(tone.IsSuspended);
        Assert.Equal(Curves.Build(ToneStyle.Contrast), pipeline.Tone);
    }

    [Fact]
    public void SuspendAndResume_Twice_ActOnce()
    {
        var (tone, pipeline) = Create();
        tone.Set(ToneStyle.Cinema);
        pipeline.Log.Clear();

        tone.Suspend();
        tone.Suspend();
        tone.Resume();
        tone.Resume();

        Assert.Equal(["tone:off", "apply", "tone:on", "apply"], pipeline.Log);
    }

    /// <summary>Panik ton eğrisini kapatır; sihirbaz sırasındaki panik sihirbaz bitince de geri gelmez.</summary>
    [Fact]
    public void Disable_TurnsTheToneOff_EvenDuringTheWizard()
    {
        var (tone, pipeline) = Create();
        tone.Set(ToneStyle.Cinema);
        Assert.True(tone.Disable());
        Assert.Null(pipeline.Tone);
        Assert.Equal(ToneStyle.Standard, tone.Style);

        tone.Set(ToneStyle.Contrast);
        tone.Suspend();
        Assert.True(tone.Disable());
        tone.Resume();

        Assert.Null(pipeline.Tone);
        Assert.Equal(ToneStyle.Standard, tone.Style);
    }

    /// <summary>Kaldırma yazılamasa da katman boş kalır: bir sonraki başarılı yazma tonsuz olur (panik ve sihirbaz ölçümü).</summary>
    [Fact]
    public void Disable_WhenTheWriteFails_StillLeavesTheLayerEmpty()
    {
        var (tone, pipeline) = Create();
        tone.Set(ToneStyle.Cinema);
        pipeline.ApplyResult = false;

        Assert.False(tone.Disable());

        Assert.Null(pipeline.Tone);
    }

    [Fact]
    public void UnknownStyle_Throws_AndWritesNothing()
    {
        var (tone, pipeline) = Create();
        Assert.Throws<ArgumentOutOfRangeException>(() => tone.Set((ToneStyle)42));
        Assert.Empty(pipeline.Log);
    }
}
