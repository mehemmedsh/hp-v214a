using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Core.Profiles;

namespace MonitorTuner.Services;

/// <summary>DDC panelindeki ayarlar. RGB kazancı üç kanaldır (16/18/1A).</summary>
public enum PanelSetting { Brightness, Contrast, ColorPreset, RedGain, GreenGain, BlueGain, Sharpness, Input, Volume }

/// <summary>Ayarın neden değiştirilemediği.</summary>
/// <remarks>
/// <see cref="WrongColorPreset"/>: RGB kazancı sadece profildeki ön ayarda (Custom RGB) etkili.
/// <see cref="BlueLightActive"/>: açık mavi ışık filtresi (T307) aynı kodları kendisi yazıyor; kapanınca kullanıcının değeri geri gelir.
/// <see cref="DimmingActive"/>: ek karartma (T306) açıkken arka ışığı tek kaydırıcı yönetir; panel onunla yarışmaz.
/// </remarks>
public enum SettingLock { None, NotWritable, WrongColorPreset, BlueLightActive, DimmingActive }

/// <param name="Name">Sabit ad (çeviri anahtarı için): OSD ön ayarı ("Neutral"), giriş ("Hdmi"/"Vga") ya da sayı.</param>
/// <param name="Selectable">Ön ayarda sadece monitörde doğrulanmış (confirmed) eşleme seçilebilir.</param>
public sealed record PanelChoice(int Value, string Name, bool Selectable);

/// <param name="Minimum">Arayüzün kaydırıcı sınırı. Kontrastta üst sınır dijital parlaklık korumasıdır.</param>
/// <param name="DeviceMaximum">Monitörün kendi üst sınırı (kontrastta Maximum'dan büyük olabilir).</param>
/// <param name="Choices">Dolu: kaydırıcı değil seçim listesi (renk ön ayarı, giriş).</param>
public sealed record PanelSettingState(
    PanelSetting Setting, byte Code, int Current, int Minimum, int Maximum, int DeviceMaximum,
    SettingLock Lock, IReadOnlyList<PanelChoice> Choices)
{
    public bool Editable => Lock == SettingLock.None;
}

/// <param name="Available">DDC var ve cevap veriyor. false: arayüz OSD talimatını gösterir.</param>
/// <param name="ExternallyChanged">Son okumadan beri monitörde (OSD) değişenler; yeni değerler alındı.</param>
public sealed record DdcPanelState(
    bool Available, IReadOnlyList<PanelSettingState> Settings, IReadOnlyList<PanelSetting> ExternallyChanged);

public enum PanelSetResult
{
    Written, Unchanged, NoDdc, NotSupported, Locked, OutOfRange,
    /// <summary>Kontrast, başlangıç değerinin (tavan) üstüne çıkarılamaz: parlaklık için arka ışık kullanılır.</summary>
    BlockedDigitalBoost,
    NotConfirmed,
    /// <summary>Kullanıcı eski değeri görürken monitörde değer değişti; yazılmadı, yeni değer alındı.</summary>
    ExternallyChanged,
    NeedsInputWarning, Throttled, Failed,
}

/// <summary>Kontrast tavanı (ayar deposu, "DdcPanel" bölümü). 0: henüz kaydedilmedi.</summary>
public sealed record DdcPanelRecord(int ContrastCeiling);

/// <summary>
/// DDC paneli (T305): parlaklık, kontrast, renk ön ayarı, RGB kazancı, keskinlik, giriş, hoparlör sesi. Arayüz
/// kaydırıcı bırakılınca bir kez <see cref="IDdcPanel.Set"/> çağırır; yazmalar <see cref="IDdcControl"/> (izin listesi,
/// yazma sınırı, Güvenlik) üzerinden gider.
/// </summary>
public interface IDdcPanel
{
    /// <summary>Monitörden okur (yazmaz). Önceki okumadan beri OSD'den değişenleri bildirir ve yeni değeri kabul eder.</summary>
    DdcPanelState Refresh();

    /// <param name="inputWarningAccepted">Giriş, kablonun bağlı olduğu porttan başka birine alınırken kullanıcı uyarıyı kabul etti mi.</param>
    PanelSetResult Set(PanelSetting setting, int value, bool inputWarningAccepted = false);
}

public sealed class DdcPanel : IDdcPanel
{
    public const string Section = "DdcPanel";

    /// <summary>Kontrast ve kazanç tabanları. Kontrast: beyaz %15'in altına inmez (<see cref="SafetyLimits.MinWhiteLevel"/>), ekran kararmaz.</summary>
    private static readonly int ContrastFloor = (int)Math.Round(SafetyLimits.MinWhiteLevel * 100);

    private const byte ColorPresetCode = 0x14;
    private const int HdmiInput = 17, VgaInput = 1;

    private static readonly IReadOnlyDictionary<PanelSetting, byte> Codes = new Dictionary<PanelSetting, byte>
    {
        [PanelSetting.Brightness] = 0x10, [PanelSetting.Contrast] = 0x12, [PanelSetting.ColorPreset] = ColorPresetCode,
        [PanelSetting.RedGain] = 0x16, [PanelSetting.GreenGain] = 0x18, [PanelSetting.BlueGain] = 0x1A,
        [PanelSetting.Sharpness] = 0x87, [PanelSetting.Input] = 0x60, [PanelSetting.Volume] = 0x62,
    };

    private readonly MonitorProfile _profile;
    private readonly IDdcControl _ddc;
    private readonly IBlueLightFilter _blueLight;
    private readonly ISettingsStore _store;
    private readonly MonitorKey _key;
    private readonly int? _connectedInput;
    private readonly Func<bool> _dimmingActive;

    private readonly Dictionary<PanelSetting, int> _known = [];
    private int _contrastCeiling;
    private bool _ceilingLoaded;

    /// <param name="dimmingActive">Ek karartma (T306 <c>IBrightnessControl.DimmingActive</c>) açık mı; bağlama kökü verir, verilmezse hep kapalı.</param>
    public DdcPanel(
        MonitorProfile profile, IDdcControl ddc, IBlueLightFilter blueLight, ISettingsStore store, MonitorKey key,
        ConnectionType connection, Func<bool>? dimmingActive = null)
    {
        _dimmingActive = dimmingActive ?? (() => false);
        _profile = profile;
        _ddc = ddc;
        _blueLight = blueLight;
        _store = store;
        _key = key;
        _connectedInput = connection switch { ConnectionType.Hdmi => HdmiInput, ConnectionType.Vga => VgaInput, _ => null };
    }

    public static byte CodeOf(PanelSetting setting) => Codes[setting];

    private static bool IsGain(PanelSetting s) => s is PanelSetting.RedGain or PanelSetting.GreenGain or PanelSetting.BlueGain;

    public DdcPanelState Refresh()
    {
        if (!_ddc.IsAvailable)
        {
            _known.Clear();
            return new DdcPanelState(false, [], []);
        }

        var preset = _ddc.Read(ColorPresetCode)?.Current;
        var changed = new List<PanelSetting>();
        var settings = Enum.GetValues<PanelSetting>()
            .Select(s => Observe(s, preset, changed)).OfType<PanelSettingState>().ToList();
        return new DdcPanelState(settings.Count > 0, settings, changed);
    }

    public PanelSetResult Set(PanelSetting setting, int value, bool inputWarningAccepted = false)
    {
        if (!_ddc.IsAvailable) return PanelSetResult.NoDdc;

        // Yazmadan önce bu ayarı yeniden oku: kullanıcı OSD'den değiştirdiyse sessizce ezme (02-MIMARI.md → Ayar sahipliği).
        var changed = new List<PanelSetting>();
        var preset = IsGain(setting) ? _ddc.Read(ColorPresetCode)?.Current : null;
        if (Observe(setting, preset, changed) is not { } state) return PanelSetResult.NotSupported;
        if (changed.Count > 0) return PanelSetResult.ExternallyChanged;
        if (!state.Editable) return PanelSetResult.Locked;

        var rejected = Validate(state, value, inputWarningAccepted);
        if (rejected is { } result) return result;
        if (value == state.Current) return PanelSetResult.Unchanged; // aynı değeri yazıp monitör belleğini yormayız

        switch (_ddc.Write(state.Code, (ushort)value))
        {
            case DdcWriteResult.Written:
                _known[setting] = value;
                return PanelSetResult.Written;
            case DdcWriteResult.NoDdc: return PanelSetResult.NoDdc;
            case DdcWriteResult.Forbidden: return PanelSetResult.NotSupported;
            case DdcWriteResult.Throttled: return PanelSetResult.Throttled;
            default: return PanelSetResult.Failed;
        }
    }

    private PanelSetResult? Validate(PanelSettingState state, int value, bool inputWarningAccepted)
    {
        if (state.Choices.Count > 0)
        {
            if (state.Choices.FirstOrDefault(c => c.Value == value) is not { } choice) return PanelSetResult.OutOfRange;
            if (state.Setting == PanelSetting.ColorPreset && !choice.Selectable) return PanelSetResult.NotConfirmed;
            if (state.Setting == PanelSetting.Input && value != state.Current && value != _connectedInput && !inputWarningAccepted)
                return PanelSetResult.NeedsInputWarning;
            return null;
        }

        // Dijital parlaklık koruması: kontrast tavanın üstüne çıkmaz (08-GORUNTU-YONTEMLERI.md → 3, Kurallar).
        if (state.Setting == PanelSetting.Contrast && value > state.Maximum && value <= state.DeviceMaximum)
            return PanelSetResult.BlockedDigitalBoost;
        return value < state.Minimum || value > state.Maximum ? PanelSetResult.OutOfRange : null;
    }

    /// <summary>Bir ayarı okur, durumunu kurar ve dış değişikliği <paramref name="changed"/>'a ekler. null: profilde ya da monitörde yok.</summary>
    private PanelSettingState? Observe(PanelSetting setting, int? preset, List<PanelSetting> changed)
    {
        var code = Codes[setting];
        var info = _profile.DdcCodes.FirstOrDefault(c => c.Code == code);
        if (info is null || _ddc.Read(code) is not { } read) return null;

        var current = read.Current;
        var deviceMax = read.Maximum > 0 ? read.Maximum : info.Maximum ?? current;
        var locked = LockFor(setting, code, preset);

        // Mavi ışık filtresi RGB kodlarını, ek karartma arka ışığı kendisi değiştirir: bunlar "dış değişiklik" sayılmaz,
        // kilit kalkınca yeniden başlanır.
        if (locked is SettingLock.BlueLightActive or SettingLock.DimmingActive) _known.Remove(setting);
        else
        {
            if (_known.TryGetValue(setting, out var seen) && seen != current) changed.Add(setting);
            _known[setting] = current;
        }

        var (min, max) = setting switch
        {
            PanelSetting.Contrast => (Math.Min(ContrastFloor, current), Math.Min(CeilingFor(current), deviceMax)),
            PanelSetting.Sharpness when info.PossibleValues.Count > 0 => (info.PossibleValues.Min(), info.PossibleValues.Max()),
            _ when IsGain(setting) => (Math.Min(GainFloor(deviceMax), current), deviceMax),
            _ => (0, deviceMax),
        };

        return new PanelSettingState(setting, code, current, min, max, deviceMax, locked, ChoicesFor(setting, info));
    }

    private SettingLock LockFor(PanelSetting setting, byte code, int? preset)
    {
        if (!_ddc.WritableCodes.Contains(code)) return SettingLock.NotWritable;
        if (setting == PanelSetting.Brightness) return _dimmingActive() ? SettingLock.DimmingActive : SettingLock.None;
        if (!IsGain(setting)) return SettingLock.None;

        var mapped = _profile.ColorPresetMap.FirstOrDefault(m => m.DdcValue == preset);
        if (mapped?.Preset != _profile.Osd.RgbGainPreset) return SettingLock.WrongColorPreset;
        return _blueLight.Kelvin < KelvinCalculator.OffKelvin && !_blueLight.IsSuspended
            ? SettingLock.BlueLightActive
            : SettingLock.None;
    }

    private IReadOnlyList<PanelChoice> ChoicesFor(PanelSetting setting, DdcCodeInfo info) => setting switch
    {
        PanelSetting.ColorPreset => [.. info.PossibleValues.Select(v =>
        {
            var m = _profile.ColorPresetMap.FirstOrDefault(x => x.DdcValue == v);
            return new PanelChoice(v, m?.Preset.ToString() ?? v.ToString(), m?.Confidence == MappingConfidence.Confirmed);
        })],
        PanelSetting.Input => [.. info.PossibleValues.Select(v =>
            new PanelChoice(v, v switch { HdmiInput => "Hdmi", VgaInput => "Vga", _ => v.ToString() }, true))],
        _ => [],
    };

    /// <summary>RGB kazancı en çok bu orana kadar düşürülebilir (T307'nin monitör tabanıyla aynı); bir kanal sıfırlanmaz.</summary>
    private static int GainFloor(int max) => (int)Math.Round(max * BlueLightFilter.MonitorMinimumCode);

    /// <summary>
    /// Kontrast tavanı: ilk okunan değer, kalıcı. Kullanıcı OSD'den yükseltirse (kendi seçimi) tavan onunla yükselir;
    /// uygulama kontrastı hiçbir zaman bu değerin üstüne çıkarmaz.
    /// </summary>
    private int CeilingFor(int current)
    {
        if (!_ceilingLoaded)
        {
            _ceilingLoaded = true;
            _contrastCeiling = _store.Read<DdcPanelRecord>(_key, Section)?.ContrastCeiling ?? 0;
        }

        if (current > _contrastCeiling)
        {
            _contrastCeiling = current;
            _store.Write(_key, Section, new DdcPanelRecord(_contrastCeiling));
        }

        return _contrastCeiling;
    }
}
