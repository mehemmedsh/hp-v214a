using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;

namespace MonitorTuner.Tests.Fakes;

/// <summary>Sahte GPU renk tablosu. Başta birim tablo; oyun, Gece ışığı ya da f.lux gibi dış yazmalar taklit edilebilir.</summary>
internal sealed class FakeGpuGammaTable(DisplayId ownDisplay) : IGpuGammaTable
{
    private readonly List<(GammaRamp Table, RestorePoint RestorePoint)> writes = [];
    private GammaRamp table = GammaRamp.Identity;

    public bool ReadFails { get; set; }

    /// <summary>true: Windows tabloyu reddeder (ör. GdiIcmGammaRange yokken derin karartma).</summary>
    public bool RejectWrites { get; set; }

    /// <summary>Sürücünün sakladığı bit sayısı; 16'dan azsa okunan tablo yazılandan biraz farklıdır.</summary>
    public int StoredBits { get; set; } = 16;

    /// <summary>Uygulamanın yazmaları; dış değişiklikler burada yok.</summary>
    public IReadOnlyList<(GammaRamp Table, RestorePoint RestorePoint)> Writes => writes;

    public GammaRamp? Read(DisplayId display) => !ReadFails && display == ownDisplay ? table : null;

    public bool Write(DisplayId display, GammaRamp newTable, RestorePoint restorePoint)
    {
        FakeRestorePoints.Check(restorePoint, WriteTarget.GpuGammaTable, display);
        ArgumentNullException.ThrowIfNull(newTable);
        if (RejectWrites || display != ownDisplay) return false;
        table = StoredBits >= 16 ? newTable : Quantize(newTable, StoredBits);
        writes.Add((newTable, restorePoint));
        return true;
    }

    private static GammaRamp Quantize(GammaRamp ramp, int bits)
    {
        var mask = (ushort)~((1 << (16 - bits)) - 1);
        return new GammaRamp(Cut(ramp.Red), Cut(ramp.Green), Cut(ramp.Blue));
        ushort[] Cut(IReadOnlyList<ushort> channel) => [.. channel.Select(v => (ushort)(v & mask))];
    }

    public void SimulateExternalChange(DisplayId display, GammaRamp newTable)
    {
        if (display != ownDisplay) throw new ArgumentException("Başka ekran.", nameof(display));
        table = newTable;
    }
}

/// <summary>Sahte kayıt defteri: <c>GdiIcmGammaRange</c>. Başta değer yok (Windows'un varsayılanı).</summary>
internal sealed class FakeGdiIcmGammaRange : IGdiIcmGammaRange
{
    private readonly List<RestorePoint> writes = [];
    private int? value;

    /// <summary>false: kullanıcı yönetici onayını (UAC) vermedi.</summary>
    public bool AdminApproves { get; set; } = true;

    public int WriteCount => writes.Count;

    public int? Read() => value;

    public bool Write(int? newValue, RestorePoint restorePoint)
    {
        FakeRestorePoints.Check(restorePoint, WriteTarget.GdiIcmGammaRange, display: null);
        if (!AdminApproves) return false;
        value = newValue;
        writes.Add(restorePoint);
        return true;
    }
}

/// <summary>Sahte NVIDIA sürücüsü: sadece dithering ve canlılık.</summary>
internal sealed class FakeNvidiaSettings(DisplayId ownDisplay) : INvidiaSettings
{
    /// <summary>Sürücü varsayılanı: dithering sürücüye bırakılmış, Digital Vibrance %50.</summary>
    public static readonly NvidiaState DriverDefault = new(DitheringMode.DriverDefault, 50);

    private readonly List<RestorePoint> writes = [];
    private NvidiaState state = DriverDefault;

    /// <summary>false: ekran NVIDIA kartında değil ya da sürücü cevap vermiyor.</summary>
    public bool Available { get; set; } = true;

    public int WriteCount => writes.Count;

    public NvidiaState? Read(DisplayId display) => Available && display == ownDisplay ? state : null;

    public bool SetDithering(DisplayId display, DitheringMode mode, RestorePoint restorePoint) =>
        Apply(display, restorePoint, state with { Dithering = mode });

    public bool SetVibrance(DisplayId display, int vibrance, RestorePoint restorePoint) =>
        Apply(display, restorePoint, state with { Vibrance = vibrance }, vibrance is >= 0 and <= 100);

    public bool ResetToDefaults(DisplayId display, RestorePoint restorePoint) => Apply(display, restorePoint, DriverDefault);

    private bool Apply(DisplayId display, RestorePoint restorePoint, NvidiaState next, bool valid = true)
    {
        FakeRestorePoints.Check(restorePoint, WriteTarget.NvidiaSettings, display);
        if (!Available || display != ownDisplay || !valid) return false;
        state = next;
        writes.Add(restorePoint);
        return true;
    }
}

/// <summary>Sahte Windows ekran modları: sadece listedeki modlar arasında geçer; mod eklemez.</summary>
internal sealed class FakeDisplayModes : IDisplayModes
{
    private readonly DisplayId ownDisplay;
    private readonly List<DisplayMode> modes;
    private readonly List<(DisplayMode Mode, RestorePoint RestorePoint)> switches = [];

    public FakeDisplayModes(DisplayId display, IEnumerable<DisplayMode> modes)
    {
        this.ownDisplay = display;
        this.modes = [.. modes];
        Current = this.modes.FirstOrDefault(m => m.RefreshHz == SafetyLimits.OriginalRefreshHz, this.modes[0]);
    }

    public DisplayMode Current { get; private set; }

    /// <summary>true: Windows geçişi reddeder.</summary>
    public bool RejectSwitches { get; set; }

    public IReadOnlyList<(DisplayMode Mode, RestorePoint RestorePoint)> Switches => switches;

    public event Action<DisplayMode>? ModeChanged;

    public IReadOnlyList<DisplayMode> ListModes(DisplayId display) => display == ownDisplay ? modes.ToList() : [];

    public DisplayMode? GetCurrentMode(DisplayId display) => display == ownDisplay ? Current : null;

    public bool SwitchMode(DisplayId display, DisplayMode mode, RestorePoint restorePoint)
    {
        FakeRestorePoints.Check(restorePoint, WriteTarget.DisplayMode, display);
        if (RejectSwitches || display != ownDisplay || !modes.Contains(mode)) return false;
        Current = mode;
        switches.Add((mode, restorePoint));
        ModeChanged?.Invoke(mode);
        return true;
    }

    /// <summary>Sürücü güncellemesi kullanıcının eklediği özel modu sildi (T209 uyarısı için).</summary>
    public void SimulateDriverDroppedMode(DisplayMode mode) => modes.Remove(mode);
}
