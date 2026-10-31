namespace MonitorTuner.Core;

/// <summary>03-GUVENLIK.md'deki pazarlıksız sayılar. Değiştirmek için önce o doküman değişir.</summary>
public static class SafetyLimits
{
    /// <summary>Hz değişikliğinden sonra onay süresi; gelmezse önceki moda dönülür.</summary>
    public static readonly TimeSpan ConfirmationTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Ek karartma beyazı en fazla bu orana indirir.</summary>
    public const double MinWhiteLevel = 0.15;

    /// <summary>Orijinal mod; hiçbir zaman silinmez veya değiştirilmez.</summary>
    public const int OriginalRefreshHz = 60;

    /// <summary>04, 05, 08 ve E6–FF'ye hiçbir zaman yazılmaz (04/05/08 fabrika sıfırlamaları, E6–FF üreticiye özel).</summary>
    public static bool IsDdcWriteForbidden(byte code) => code is 0x04 or 0x05 or 0x08 or >= 0xE6;
}
