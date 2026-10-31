using MonitorTuner.Core;

namespace MonitorTuner.Hardware.Abstractions;

public readonly record struct DisplayMode(int Width, int Height, int RefreshHz);

/// <summary>
/// Windows ekran katmanı: sadece Windows'un listesindeki modlar arasında geçer. Mod eklemez, silmez,
/// değiştirmez (modları kullanıcı NVIDIA özel çözünürlüğüyle ekler; orijinal 60 Hz'e dokunulmaz).
/// </summary>
public interface IDisplayModes
{
    IReadOnlyList<DisplayMode> ListModes(DisplayId display);

    DisplayMode? GetCurrentMode(DisplayId display);

    /// <returns>Mod listede yoksa veya Windows reddederse false.</returns>
    bool SwitchMode(DisplayId display, DisplayMode mode, RestorePoint restorePoint);
}
