using MonitorTuner.Services;

namespace MonitorTuner.Tests.Services;

/// <summary>
/// Ayar sihirbazı desenleri (T312, 08-GORUNTU-YONTEMLERI.md → Renk düzeltme). Desenler saf veridir; tam ekran pencere
/// (App) sadece çizer. Kullanıcı kareler arasında elle gezer: desenlerde zamanlayıcı ya da yanıp sönme yoktur (03-GUVENLIK.md → 7).
/// </summary>
public class WizardPatternsTests
{
    private static Pattern Single(WizardStep step, PatternKind kind) =>
        WizardPatterns.For(step).Single(p => p.Kind == kind);

    private static IEnumerable<Pattern> All() =>
        Enum.GetValues<WizardStep>().SelectMany(WizardPatterns.For);

    [Fact]
    public void Steps_MapToTheirPatterns()
    {
        Assert.Empty(WizardPatterns.For(WizardStep.WarmUpAndPosition)); // metin adımı
        Assert.Equal([PatternKind.BlackClipping, PatternKind.WhiteClipping], WizardPatterns.For(WizardStep.BlackAndWhiteClipping).Select(p => p.Kind));
        Assert.Equal([PatternKind.Sharpness], WizardPatterns.For(WizardStep.Sharpness).Select(p => p.Kind));
        Assert.Equal([PatternKind.FullRangeSteps], WizardPatterns.For(WizardStep.FullRgbCheck).Select(p => p.Kind));
        Assert.Equal([PatternKind.ViewingAngle], WizardPatterns.For(WizardStep.ViewingAngle).Select(p => p.Kind));
        Assert.Equal([PatternKind.DeadPixels, PatternKind.Uniformity], WizardPatterns.For(WizardStep.DeadPixelsAndUniformity).Select(p => p.Kind));
        Assert.Equal([PatternKind.WhiteField], WizardPatterns.For(WizardStep.WhiteMatch).Select(p => p.Kind));
        Assert.Empty(WizardPatterns.For(WizardStep.Gamma)); // T313
        Assert.Empty(WizardPatterns.For(WizardStep.GreyBalance));
    }

    [Fact]
    public void BlackClipping_ShowsLevels1To25_OnBlack()
    {
        var frame = Single(WizardStep.BlackAndWhiteClipping, PatternKind.BlackClipping).Frames.Single();

        Assert.Equal(new Rgb(0, 0, 0), frame.Background);
        Assert.Equal(Enumerable.Range(1, 25), frame.Patches.Select(p => p.Level!.Value));
        Assert.All(frame.Patches, p => Assert.Equal(new Rgb((byte)p.Level!.Value, (byte)p.Level.Value, (byte)p.Level.Value), p.Color));
    }

    [Fact]
    public void WhiteClipping_ShowsLevels230To254_OnWhite()
    {
        var frame = Single(WizardStep.BlackAndWhiteClipping, PatternKind.WhiteClipping).Frames.Single();

        Assert.Equal(new Rgb(255, 255, 255), frame.Background);
        Assert.Equal(Enumerable.Range(230, 25), frame.Patches.Select(p => p.Level!.Value));
    }

    /// <summary>RGB Full doğruysa 1–16 siyahtan ayrı görünür; Limited'ta hepsi siyaha ezilir.</summary>
    [Fact]
    public void FullRangeSteps_CoverTheLimitedRangeFootroom()
    {
        var frame = Single(WizardStep.FullRgbCheck, PatternKind.FullRangeSteps).Frames.Single();

        Assert.Equal(new Rgb(0, 0, 0), frame.Background);
        Assert.Equal(Enumerable.Range(1, 16), frame.Patches.Select(p => p.Level!.Value));
    }

    [Fact]
    public void Sharpness_HasDarkOnLightAndLightOnDarkText_InSeveralSizes()
    {
        var frames = Single(WizardStep.Sharpness, PatternKind.Sharpness).Frames;

        Assert.Equal(2, frames.Count);
        Assert.All(frames, f => Assert.True(f.Text is { Sizes.Count: >= 3 }));
        Assert.Equal((new Rgb(255, 255, 255), new Rgb(0, 0, 0)), (frames[0].Background, frames[0].Text!.Color));
        Assert.Equal((new Rgb(0, 0, 0), new Rgb(255, 255, 255)), (frames[1].Background, frames[1].Text!.Color));
    }

    [Fact]
    public void ViewingAngle_IsMidGrey_WithTopMiddleAndBottomBandsOfTheSameGrey()
    {
        var frame = Single(WizardStep.ViewingAngle, PatternKind.ViewingAngle).Frames.Single();

        Assert.Equal(new Rgb(128, 128, 128), frame.Background);
        Assert.Equal(3, frame.Patches.Count);
        Assert.All(frame.Patches, p => Assert.Equal(frame.Background, p.Color));
        Assert.True(frame.Patches[0].Y < frame.Patches[1].Y && frame.Patches[1].Y < frame.Patches[2].Y); // üst, orta, alt
    }

    [Fact]
    public void DeadPixels_CyclesBlackWhiteRedGreenBlue_FullScreen()
    {
        var frames = Single(WizardStep.DeadPixelsAndUniformity, PatternKind.DeadPixels).Frames;

        Assert.Equal(
            [new Rgb(0, 0, 0), new Rgb(255, 255, 255), new Rgb(255, 0, 0), new Rgb(0, 255, 0), new Rgb(0, 0, 255)],
            frames.Select(f => f.Background));
        Assert.All(frames, f => Assert.Empty(f.Patches));
    }

    [Fact]
    public void Uniformity_ShowsThreeFullScreenGreys()
    {
        var frames = Single(WizardStep.DeadPixelsAndUniformity, PatternKind.Uniformity).Frames;

        Assert.Equal([51, 128, 204], frames.Select(f => (int)f.Background.R));
        Assert.All(frames, f => Assert.True(f.Background.R == f.Background.G && f.Background.G == f.Background.B));
    }

    [Fact]
    public void WhiteField_IsPureWhite() =>
        Assert.Equal(new Rgb(255, 255, 255), Single(WizardStep.WhiteMatch, PatternKind.WhiteField).Frames.Single().Background);

    [Fact]
    public void EveryPatch_StaysInsideTheScreen_AndPatchesDoNotOverlap()
    {
        foreach (var frame in All().SelectMany(p => p.Frames))
        {
            Assert.All(frame.Patches, p =>
            {
                Assert.InRange(p.X, 0, 1);
                Assert.InRange(p.Y, 0, 1);
                Assert.True(p.Width > 0 && p.Height > 0 && p.X + p.Width <= 1.0001 && p.Y + p.Height <= 1.0001);
            });
            for (var i = 0; i < frame.Patches.Count; i++)
                for (var j = i + 1; j < frame.Patches.Count; j++)
                {
                    var a = frame.Patches[i];
                    var b = frame.Patches[j];
                    var overlap = a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
                    Assert.False(overlap, $"Yamalar çakışıyor: {i} ve {j}");
                }
        }
    }

    /// <summary>Işığa duyarlılık: desen verisinde süre, aralık ya da frekans yok; kareleri kullanıcı elle değiştirir.</summary>
    [Fact]
    public void Patterns_HaveNoTimingMembers()
    {
        var types = new[] { typeof(Pattern), typeof(PatternFrame), typeof(PatternPatch), typeof(PatternText) };
        var names = types.SelectMany(t => t.GetProperties()).Select(p => p.Name).ToList();

        Assert.DoesNotContain(names, n => n.Contains("Interval") || n.Contains("Duration") || n.Contains("Delay") || n.Contains("Hz") || n.Contains("Flash"));
        Assert.All(All(), p => Assert.NotEmpty(p.Frames));
    }
}
