using MonitorTuner.Color;

namespace MonitorTuner.Services;

public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static Rgb Grey(int level) => new((byte)level, (byte)level, (byte)level);
}

public enum PatternKind { BlackClipping, WhiteClipping, Sharpness, FullRangeSteps, ViewingAngle, DeadPixels, Uniformity, WhiteField, Gamma, GreyBalance }

/// <summary>
/// Ekranın yüzdesi olarak (0–1) bir dikdörtgen. <paramref name="Level"/>: gri yamanın seviyesi (0–255), etiket için.
/// <paramref name="StripeColor"/> verilirse yama düz değil, satır satır (1 fiziksel piksel) <paramref name="Color"/> ve bu renkle dolar (gamma deseni).
/// </summary>
public sealed record PatternPatch(double X, double Y, double Width, double Height, Rgb Color, int? Level = null, Rgb? StripeColor = null);

/// <summary>Örnek yazı: <paramref name="Sizes"/> punto olarak, her biri bir satır.</summary>
public sealed record PatternText(Rgb Color, IReadOnlyList<int> Sizes);

/// <summary>Tam ekran bir kare: arka plan, üstüne yamalar ve isteğe bağlı örnek yazı.</summary>
public sealed record PatternFrame(Rgb Background, IReadOnlyList<PatternPatch> Patches, PatternText? Text = null);

/// <summary>
/// Bir desen, kullanıcının elle gezdiği kareler listesidir. Süre, aralık ya da yanıp sönme yoktur (03-GUVENLIK.md → 7);
/// kareyi değiştirmek pencerenin (App) işi ve kullanıcının eylemidir.
/// </summary>
public sealed record Pattern(PatternKind Kind, IReadOnlyList<PatternFrame> Frames);

/// <summary>Ayar sihirbazının desenleri (T312: monitör ayarları; T313: gamma ve gri dengesi).</summary>
public static class WizardPatterns
{
    private static readonly Rgb Black = new(0, 0, 0), White = new(255, 255, 255);

    public static IReadOnlyList<Pattern> For(WizardStep step) => step switch
    {
        WizardStep.BlackAndWhiteClipping => [Row(PatternKind.BlackClipping, Black, Range(1, 25)), Row(PatternKind.WhiteClipping, White, Range(230, 25))],
        WizardStep.Sharpness => [Sharpness()],
        WizardStep.FullRgbCheck => [Row(PatternKind.FullRangeSteps, Black, Range(1, 16))],
        WizardStep.ViewingAngle => [ViewingAngle()],
        WizardStep.DeadPixelsAndUniformity =>
        [
            Fields(PatternKind.DeadPixels, Black, White, new Rgb(255, 0, 0), new Rgb(0, 255, 0), new Rgb(0, 0, 255)),
            Fields(PatternKind.Uniformity, Rgb.Grey(51), Rgb.Grey(128), Rgb.Grey(204)), // %20, %50, %80
        ],
        WizardStep.WhiteMatch => [Fields(PatternKind.WhiteField, White)],
        WizardStep.Gamma => [Gamma()],
        WizardStep.GreyBalance => [Fields(PatternKind.GreyBalance, Rgb.Grey(CalibrationCurves.DarkLevel), Rgb.Grey(CalibrationCurves.MidLevel), Rgb.Grey(CalibrationCurves.LightLevel))],
        _ => [], // metin adımları (ısınma, konum)
    };

    /// <summary>Panel gamması adayları (1,8 … 2,8); gamma deseninin her karesi sırayla bir adaydır.</summary>
    public static IReadOnlyList<double> GammaCandidates { get; } = [.. Enumerable.Range(18, 11).Select(n => n / 10.0)];

    private static IEnumerable<int> Range(int start, int count) => Enumerable.Range(start, count);

    /// <summary>Arka plana yakın seviyelerde yan yana yamalar: hangisinin hâlâ ayırt edildiği okunur.</summary>
    private static Pattern Row(PatternKind kind, Rgb background, IEnumerable<int> levels)
    {
        var list = levels.ToList();
        var slot = 0.9 / list.Count;
        var patches = list.Select((level, i) => new PatternPatch(0.05 + i * slot, 0.35, slot * 0.9, 0.3, Rgb.Grey(level), level));
        return new Pattern(kind, [new PatternFrame(background, [.. patches])]);
    }

    /// <summary>Yazı kenarında hale (beyaz çizgi) aranır: koyu zemin üstünde açık ve açık zemin üstünde koyu yazı.</summary>
    private static Pattern Sharpness()
    {
        int[] sizes = [9, 11, 14, 18, 24];
        return new Pattern(PatternKind.Sharpness,
        [
            new PatternFrame(White, [], new PatternText(Black, sizes)),
            new PatternFrame(Black, [], new PatternText(White, sizes)),
        ]);
    }

    /// <summary>%50 gri; üst, orta ve alt şeritler aynı gri. Gözün konumu doğruysa üçü eşit görünür.</summary>
    private static Pattern ViewingAngle()
    {
        var grey = Rgb.Grey(128);
        PatternPatch Band(double y) => new(0.1, y, 0.8, 0.12, grey, 128);
        return new Pattern(PatternKind.ViewingAngle, [new PatternFrame(grey, [Band(0.02), Band(0.44), Band(0.86)])]);
    }

    /// <summary>
    /// Siyah-beyaz çizgili alan ışığın tam yarısını verir (gamma'dan bağımsız); yanındaki düz gri, aday gamma'da %50 ışığı veren
    /// seviyedir (255 · 0,5^(1/γ)). Kullanıcı uzaktan çizgili alanla en eşit görünen kareyi seçer: o kare panel gamması.
    /// </summary>
    private static Pattern Gamma() => new(PatternKind.Gamma,
    [
        .. GammaCandidates.Select(g =>
        {
            var level = (int)Math.Round(255 * Math.Pow(0.5, 1 / g));
            return new PatternFrame(Black,
            [
                new PatternPatch(0.1, 0.25, 0.4, 0.5, Black, StripeColor: White),
                new PatternPatch(0.5, 0.25, 0.4, 0.5, Rgb.Grey(level), level),
            ]);
        }),
    ]);

    private static Pattern Fields(PatternKind kind, params Rgb[] colors) =>
        new(kind, [.. colors.Select(c => new PatternFrame(c, []))]);
}
