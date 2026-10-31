using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;

namespace MonitorTuner.Services;

/// <summary>Birleştirme sırası: değer sırası katman sırasıdır.</summary>
public enum ColorLayer { Calibration, BlueLight, Dimming }

/// <summary>Renk boru hattı (T302): GPU renk tablosunun tek yazarı.</summary>
public interface IColorPipeline
{
    /// <param name="table">Katmanın aktarım tablosu; null: katman kapalı.</param>
    void SetLayer(ColorLayer layer, GammaRamp? table);

    /// <summary>Katmanları birleştirip tabloyu (geri alma noktasıyla) yazar.</summary>
    bool Apply();

    /// <summary>Dış program tabloyu değiştirdiyse kullanıcının tablosunu geri yükler; true ise uyarı gösterilir.</summary>
    bool RestoreIfChangedExternally();

    /// <summary>GdiIcmGammaRange'i 256 yapar (tek seferlik yönetici onayı, eski değer yedeklenir).</summary>
    bool EnsureFullGammaRange();

    /// <summary>Normal kapanış: tabloda sadece kalibrasyon kalır.</summary>
    void Shutdown();

    /// <summary>Kurtarma ve kaldırma: birim tablo.</summary>
    void ResetTable();
}

/// <summary>İskelet (T002). Gövdeyi T302 yazar; mimari testi GPU tablosu yazarı olarak sadece bu tipe izin verir.</summary>
public sealed class ColorPipeline(
    DisplayId display,
    IGpuGammaTable gpuTable,
    IGdiIcmGammaRange gammaRange,
    IGammaTableCombiner combiner,
    ISafetyLayer safety) : IColorPipeline, IGpuTableOwner
{
    private readonly DisplayId _display = display;
    private readonly IGpuGammaTable _gpuTable = gpuTable;
    private readonly IGdiIcmGammaRange _gammaRange = gammaRange;
    private readonly IGammaTableCombiner _combiner = combiner;
    private readonly ISafetyLayer _safety = safety;

    public void SetLayer(ColorLayer layer, GammaRamp? table) => throw Todo();
    public bool Apply() => throw Todo();
    public bool RestoreIfChangedExternally() => throw Todo();
    public bool EnsureFullGammaRange() => throw Todo();
    public void Shutdown() => throw Todo();
    public void ResetTable() => throw Todo();
    public void Restore(DisplayId display, GammaRamp table) => throw Todo();

    private static NotImplementedException Todo() => new("T302 yazacak.");
}
