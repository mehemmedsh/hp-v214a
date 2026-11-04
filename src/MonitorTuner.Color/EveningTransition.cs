namespace MonitorTuner.Color;

/// <summary>
/// İsteğe bağlı akşam geçişi (T307): 6500K'dan hedefe 30–60 dakikada, en az 5 dakikalık adımlarla. Adımlar mired
/// (1/Kelvin) üzerinde eşit aralıklıdır; algı bu ölçekte eşit ilerler. Adım azlığı DDC yazmalarını da azaltır
/// (03-GUVENLIK.md → 3). Sadece renk; arka ışıkla ilgisi yok.
/// </summary>
public static class EveningTransition
{
    public const int MinStepMinutes = 5;
    public const int MinDurationMinutes = 30;
    public const int MaxDurationMinutes = 60;

    public static int StepCount(TimeSpan duration) =>
        Math.Max(1, (int)Math.Ceiling(duration.TotalMinutes / MinStepMinutes));

    /// <summary>Geçişin başından <paramref name="elapsed"/> sonra uygulanacak Kelvin (10K'ya yuvarlı). Başlamadan 6500, bitince hedef.</summary>
    public static int KelvinAt(TimeSpan elapsed, TimeSpan duration, int targetKelvin)
    {
        var target = Math.Clamp(targetKelvin, KelvinCalculator.MinKelvin, KelvinCalculator.OffKelvin);
        var steps = StepCount(duration);
        var stepLength = duration / steps;
        var step = elapsed <= TimeSpan.Zero ? 0 : (int)Math.Min(steps, Math.Floor(elapsed / stepLength));

        var from = 1e6 / KelvinCalculator.OffKelvin;
        var to = 1e6 / target;
        var kelvin = 1e6 / (from + (to - from) * step / steps);
        return (int)(Math.Round(kelvin / 10) * 10);
    }
}
