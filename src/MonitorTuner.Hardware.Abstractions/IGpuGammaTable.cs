using MonitorTuner.Core;

namespace MonitorTuner.Hardware.Abstractions;

/// <summary>GPU renk tablosu (Windows gamma tablosu). Tek yazarı renk boru hattı.</summary>
public interface IGpuGammaTable
{
    /// <summary>Ekrandaki tablo; dış değişiklik tespiti için. null: okunamadı.</summary>
    GammaRamp? Read(DisplayId display);

    [OnlyColorPipelineMayCall]
    bool Write(DisplayId display, GammaRamp table, RestorePoint restorePoint);
}

/// <summary>
/// <c>HKLM\…\ICM\GdiIcmGammaRange</c>: Windows'un derin karartma ve sıcak renk tablolarını reddetmemesi için 256.
/// Yazma tek seferlik yönetici onayı ister; eski değer önce yedeklenir (Güvenlik).
/// </summary>
public interface IGdiIcmGammaRange
{
    /// <summary>null: değer yok.</summary>
    int? Read();

    /// <param name="value">null: değeri sil (eski hâli "yok" ise geri dönüş).</param>
    bool Write(int? value, RestorePoint restorePoint);
}
