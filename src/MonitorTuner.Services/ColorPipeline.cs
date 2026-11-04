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
    /// <param name="table">Katmanın aktarım tablosu; null: katman kapalı. Donanıma dokunmaz, <see cref="Apply"/> yazar.</param>
    void SetLayer(ColorLayer layer, GammaRamp? table);

    /// <summary>
    /// Katmanları birleştirip tabloyu (geri alma noktasıyla) yazar; tablo zaten ekrandaysa yazmaz. false: Windows
    /// tabloyu reddetti ya da tablo okunamadı (yedeksiz yazılmaz); ekranda kullanıcının son tablosu kalır.
    /// </summary>
    bool Apply();

    /// <summary>
    /// Dış program tabloyu değiştirdiyse kullanıcının tablosunu geri yükler; true ise uyarı gösterilir (geri yükleme
    /// reddedilse de). Uygulama henüz tablo yazmadıysa ya da tablo okunamıyorsa false.
    /// </summary>
    bool RestoreIfChangedExternally();

    /// <summary>GdiIcmGammaRange'i 256 yapar (tek seferlik yönetici onayı, eski değer yedeklenir).</summary>
    bool EnsureFullGammaRange();

    /// <summary>Normal kapanış: tabloda sadece kalibrasyon kalır.</summary>
    void Shutdown();

    /// <summary>Kurtarma ve kaldırma: birim tablo.</summary>
    void ResetTable();
}

/// <summary>
/// Renk boru hattı (T302). Kullanıcının tablosu, uygulamanın en son yazdığı (ya da Güvenlik'in geri yüklediği) tablodur;
/// dış değişiklik tespiti ve geri yükleme ona göre yapılır. Güvenlik geri alırken <see cref="Restore"/>'u kendi
/// zamanlayıcısından çağırabildiği için durum kilitle korunur; kilit sırası her zaman boru hattı → Güvenlik.
/// </summary>
public sealed class ColorPipeline(
    DisplayId display,
    IGpuGammaTable gpuTable,
    IGdiIcmGammaRange gammaRange,
    IGammaTableCombiner combiner,
    ISafetyLayer safety) : IColorPipeline, IGpuTableOwner
{
    /// <summary>Windows'un derin karartma ve çok sıcak renk tablolarını reddetmediği <c>GdiIcmGammaRange</c> değeri.</summary>
    public const int FullGammaRange = 256;

    private readonly DisplayId _display = display;
    private readonly IGpuGammaTable _gpuTable = gpuTable;
    private readonly IGdiIcmGammaRange _gammaRange = gammaRange;
    private readonly IGammaTableCombiner _combiner = combiner;
    private readonly ISafetyLayer _safety = safety;

    private readonly Lock _gate = new();
    private readonly SortedDictionary<ColorLayer, GammaRamp> _layers = [];
    private GammaRamp? _userTable; // null: uygulama henüz tablo yazmadı, dokunulacak bilinen tablo yok
    private GammaRamp? _observed;  // yazmadan hemen sonra okunan; sürücü değerleri yuvarlayabilir

    public void SetLayer(ColorLayer layer, GammaRamp? table)
    {
        if (!Enum.IsDefined(layer)) throw new ArgumentOutOfRangeException(nameof(layer), layer, null);
        lock (_gate)
        {
            if (table is null) _layers.Remove(layer);
            else _layers[layer] = table;
        }
    }

    public bool Apply()
    {
        lock (_gate) return WriteUserTable(Combined());
    }

    public bool RestoreIfChangedExternally()
    {
        lock (_gate)
        {
            if (_userTable is null) return false;
            var now = _gpuTable.Read(_display);
            if (now is null || now.Equals(_observed)) return false;
            Write(_userTable); // Kural 1, 2. istisna: yeni değer seçilmez, kullanıcının tablosuna dönülür
            return true;
        }
    }

    public bool EnsureFullGammaRange()
    {
        if (_gammaRange.Read() == FullGammaRange) return true; // onay bir kez istenir
        try
        {
            // Güvenlik eski değeri (yoksa "değer yok") yedekler; kaldırmada o değere döner.
            var point = _safety.CreateRestorePoint(new WriteRequest(WriteTarget.GdiIcmGammaRange, _display));
            return _gammaRange.Write(FullGammaRange, point);
        }
        catch (InvalidOperationException)
        {
            return false; // ilk açılış yedeği alınamadı
        }
    }

    public void Shutdown()
    {
        lock (_gate)
        {
            foreach (var layer in _layers.Keys.Where(l => l != ColorLayer.Calibration).ToList()) _layers.Remove(layer);
            WriteUserTable(Combined()); // en iyi çaba: okunamazsa yazılmaz, kalan tabloyu kurtarma dosyası sıfırlar
        }
    }

    public void ResetTable()
    {
        lock (_gate)
        {
            _layers.Clear();
            WriteUserTable(GammaRamp.Identity);
        }
    }

    /// <summary>Güvenlik'in geri alması (onay süresi, fabrika ayarı, kaldırma): geri yüklenen tablo kullanıcının tablosu olur.</summary>
    public void Restore(DisplayId display, GammaRamp table)
    {
        if (display != _display) throw new ArgumentException($"Boru hattı sadece {_display.Value} tablosunu yazar.", nameof(display));
        ArgumentNullException.ThrowIfNull(table);
        lock (_gate) Write(table);
    }

    private GammaRamp Combined() => _layers.Count == 0 ? GammaRamp.Identity : _combiner.Combine([.. _layers.Values]);

    /// <summary>Tablo zaten ekrandaysa yazmaz (gereksiz geri alma noktası olmasın), sadece sahiplenir.</summary>
    private bool WriteUserTable(GammaRamp table)
    {
        var now = _gpuTable.Read(_display);
        if (now is not null && (now.Equals(table) || (table.Equals(_userTable) && now.Equals(_observed))))
        {
            (_userTable, _observed) = (table, now);
            return true;
        }
        return Write(table);
    }

    private bool Write(GammaRamp table)
    {
        RestorePoint point;
        try
        {
            point = _safety.CreateRestorePoint(new WriteRequest(WriteTarget.GpuGammaTable, _display));
        }
        catch (InvalidOperationException)
        {
            return false; // tablo okunamadı: yedeksiz yazılmaz
        }
        if (!_gpuTable.Write(_display, table, point)) return false;
        _userTable = table;
        _observed = _gpuTable.Read(_display) ?? table;
        return true;
    }
}
