using MonitorTuner.Core;

namespace MonitorTuner.Color;

/// <summary>
/// Karartma planlayıcı (T306, 08-GORUNTU-YONTEMLERI.md → 3). Tek kaydırıcı iki kademe:
/// <list type="bullet">
/// <item>[<see cref="TransitionPoint"/>, 1]: arka ışık (DDC) 0 → 100, GPU ölçeği 1. En iyi yol: kontrast ve seviye kaybı yok.</item>
/// <item>[0, <see cref="TransitionPoint"/>]: arka ışık 0'da kalır, GPU ölçeği 1 → <see cref="SafetyLimits.MinWhiteLevel"/>.</item>
/// </list>
/// İki kademe aynı anda çalışmaz (ek karartma sadece arka ışık tabanının altında). Ölçek doğrusal ışıktır (beyaz seviyesi);
/// tabloya <see cref="GammaRamps.Scale"/> çevirir. Taban hiçbir girdiyle aşılmaz.
/// </summary>
public sealed class DimmingPlanner : IDimmingPlanner
{
    /// <summary>
    /// Kaydırıcının alt bölümü (ek karartma) buraya kadar uzanır. Arayüz iki bölümü bu noktada renkle ayırır.
    /// </summary>
    public const double TransitionPoint = 0.2;

    /// <summary>Arka ışığın DDC aralığı: 0–100 (monitörün en büyük değerine servis katmanı çevirir).</summary>
    public const int MaxBacklight = 100;

    public DimmingPlan Plan(double slider)
    {
        if (double.IsNaN(slider)) throw new ArgumentOutOfRangeException(nameof(slider), "Kaydırıcı sayı olmalı.");

        var s = Math.Clamp(slider, 0.0, 1.0);
        if (s >= TransitionPoint)
        {
            var backlight = Math.Round(MaxBacklight * (s - TransitionPoint) / (1 - TransitionPoint), MidpointRounding.AwayFromZero);
            return new DimmingPlan((int)Math.Clamp(backlight, 0, MaxBacklight), 1.0);
        }

        var scale = SafetyLimits.MinWhiteLevel + (1 - SafetyLimits.MinWhiteLevel) * (s / TransitionPoint);
        return new DimmingPlan(0, Math.Clamp(scale, SafetyLimits.MinWhiteLevel, 1.0));
    }

    /// <summary>Arka ışık değerinin (0–100) kaydırıcıdaki yeri; <see cref="Plan"/>'ın tersi. Açılışta kaydırıcıyı kurmak için.</summary>
    public static double SliderForBacklight(int backlight) =>
        TransitionPoint + (1 - TransitionPoint) * Math.Clamp(backlight, 0, MaxBacklight) / MaxBacklight;
}
