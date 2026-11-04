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

    /// <summary>
    /// Anlık geçiş, Windows'a kaydedilmez: uygulama 15 sn içinde çökerse yeniden başlatmada kayıtlı mod gelir.
    /// Güvenlik'in geri dönüşü de bununla yapılır (kayıtlı mod zaten öncekidir).
    /// </summary>
    /// <returns>Mod listede yoksa veya Windows reddederse false.</returns>
    bool SwitchMode(DisplayId display, DisplayMode mode, RestorePoint restorePoint);

    /// <summary>
    /// Şu anki modu Windows'a kalıcı kaydeder (yeniden başlatma ve monitör uyanması sonrası da gelir). Geçiş yapmaz.
    /// Hz paneli bunu sadece kullanıcı onayından sonra çağırır.
    /// </summary>
    /// <returns><paramref name="mode"/> şu anki mod değilse veya Windows reddederse false.</returns>
    bool PersistMode(DisplayId display, DisplayMode mode, RestorePoint restorePoint);
}
