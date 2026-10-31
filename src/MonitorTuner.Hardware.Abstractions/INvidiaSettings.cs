using MonitorTuner.Core;

namespace MonitorTuner.Hardware.Abstractions;

public enum DitheringMode { DriverDefault, On, Off }

/// <param name="Vibrance">NVIDIA Digital Vibrance, 0–100 (NVIDIA panelindeki %).</param>
public readonly record struct NvidiaState(DitheringMode Dithering, int Vibrance);

/// <summary>NVIDIA katmanı: sadece dithering ve canlılık. Güç, saat, voltaj ve fana dokunan üye yok ve eklenmez.</summary>
public interface INvidiaSettings
{
    NvidiaState? Read(DisplayId display);

    bool SetDithering(DisplayId display, DitheringMode mode, RestorePoint restorePoint);

    bool SetVibrance(DisplayId display, int vibrance, RestorePoint restorePoint);

    /// <summary>Dithering ve canlılığı sürücü varsayılanına döndürür.</summary>
    bool ResetToDefaults(DisplayId display, RestorePoint restorePoint);
}
