using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;

namespace MonitorTuner.Services;

/// <summary>Renk boru hattının dithering'e haber verdiği yer (T303).</summary>
public interface IDitheringPolicy
{
    /// <param name="inUse">Ekrandaki tablo 8 bit seviyelerini değiştiriyor mu (kalibrasyon ya da efekt).</param>
    void TableInUse(bool inUse);
}

/// <summary>NVIDIA ayarları (T303): dithering ve canlılık. Güç, saat, voltaj ve fana dokunmaz.</summary>
public interface INvidiaControl
{
    /// <summary>İstenen canlılık (NVIDIA panelindeki %); null: kapalı, kullanıcının değeri.</summary>
    int? Vibrance { get; }

    /// <summary>Film ve Oyun profilleri (T308). Askıdayken saklanır, <see cref="Resume"/>'da yazılır. false: okunamadı ya da reddedildi.</summary>
    bool SetVibrance(int percent);

    /// <summary>Panik kısayolu ve Film/Oyun dışı profiller: kullanıcının değerine döner. Dithering'e dokunmaz.</summary>
    bool ResetVibrance();

    /// <summary>Ayar sihirbazı: canlılık geçici kapanır.</summary>
    void Suspend();

    void Resume();

    /// <summary>Normal kapanış: dithering ve canlılık kullanıcının değerine döner.</summary>
    void Shutdown();
}

/// <summary>
/// NVIDIA ayarları (T303). Kullanıcının değeri: ilk açılış yedeğindeki değer (çökmeden kalan değerler sayılmaz), yoksa
/// uygulamanın ilk okuduğu. Uygulama sadece kendi açtığını geri alır; dokunmadığı ayarı yazmaz. Her yazma Güvenlik'ten
/// geri alma noktası alır; değer zaten yerindeyse yazmaz. Kilit sırası: boru hattı → bu sınıf → Güvenlik.
/// </summary>
public sealed class NvidiaControl(DisplayId display, INvidiaSettings nvidia, ISafetyLayer safety, IFactoryBackupStore factory)
    : INvidiaControl, IDitheringPolicy
{
    private readonly Lock _gate = new();
    private NvidiaState? _original;
    private int? _vibrance;
    private bool _tableInUse, _suspended, _ditheringOwned, _vibranceOwned;

    public int? Vibrance
    {
        get { lock (_gate) return _vibrance; }
    }

    public void TableInUse(bool inUse)
    {
        lock (_gate)
        {
            _tableInUse = inUse;
            SyncDithering();
        }
    }

    public bool SetVibrance(int percent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(percent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 100);
        lock (_gate)
        {
            _vibrance = percent;
            return SyncVibrance();
        }
    }

    public bool ResetVibrance()
    {
        lock (_gate)
        {
            _vibrance = null;
            return SyncVibrance();
        }
    }

    public void Suspend()
    {
        lock (_gate)
        {
            _suspended = true;
            SyncVibrance();
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            _suspended = false;
            SyncVibrance();
        }
    }

    public void Shutdown()
    {
        lock (_gate)
        {
            (_tableInUse, _vibrance, _suspended) = (false, null, false);
            SyncDithering(); // en iyi çaba: dönemeyeni kurtarma dosyası varsayılana alır (T104)
            SyncVibrance();
        }
    }

    // NVIDIA tam aralık RGB'de varsayılan olarak dithering yapmaz; tablo seviye kaybettirirken açık tutulur.
    private bool SyncDithering()
    {
        if (_tableInUse)
        {
            _ditheringOwned = true;
            return Set(s => s.Dithering == DitheringMode.On, p => nvidia.SetDithering(display, DitheringMode.On, p));
        }
        if (!_ditheringOwned) return true;
        var target = _original?.Dithering ?? DitheringMode.DriverDefault;
        if (!Set(s => s.Dithering == target, p => nvidia.SetDithering(display, target, p))) return false;
        _ditheringOwned = false;
        return true;
    }

    private bool SyncVibrance()
    {
        if (_vibrance is { } wanted && !_suspended)
        {
            _vibranceOwned = true;
            return Set(s => s.Vibrance == wanted, p => nvidia.SetVibrance(display, wanted, p));
        }
        if (!_vibranceOwned) return true;
        var target = _original?.Vibrance ?? DriverDefaultVibrance;
        if (!Set(s => s.Vibrance == target, p => nvidia.SetVibrance(display, target, p))) return false;
        _vibranceOwned = false;
        return true;
    }

    /// <summary>NVIDIA panelindeki varsayılan canlılık (%50); kullanıcının değeri hiç okunamadıysa.</summary>
    private const int DriverDefaultVibrance = 50;

    private bool Set(Func<NvidiaState, bool> done, Func<RestorePoint, bool> write)
    {
        if (nvidia.Read(display) is not { } now) return false;
        _original ??= factory.Load()?.Nvidia ?? now;
        if (done(now)) return true;
        RestorePoint point;
        try
        {
            point = safety.CreateRestorePoint(new WriteRequest(WriteTarget.NvidiaSettings, display));
        }
        catch (InvalidOperationException)
        {
            return false; // yedek alınamadı: yedeksiz yazılmaz
        }
        return write(point);
    }
}
