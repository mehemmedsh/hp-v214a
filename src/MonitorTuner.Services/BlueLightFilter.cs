using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Core.Profiles;

namespace MonitorTuner.Services;

/// <summary>Mavi ışık filtresiyle aynı anda çalışmaması gereken programlar.</summary>
public enum ColorConflict { WindowsNightLight, Flux }

/// <summary>Gece ışığı / f.lux açık mı bakar (Windows tarafını sonra yazılacak Hardware/Composition sağlar).</summary>
public interface IColorConflictProbe
{
    IReadOnlyList<ColorConflict> Detect();
}

/// <summary>
/// Filtre açıkken kaydedilen baz ve son yazılan kazanç kodları (ayar deposu, "BlueLightBase" bölümü). Uygulama çökerse
/// ya da kapanırsa sonraki açılış düşük kodları baz saymaz, kayıtlı baza döner. Boş diziler: kayıt yok.
/// </summary>
public sealed record BlueLightBaseRecord(ushort[] Base, ushort[] Applied);

public enum LowBlueLightResult { Selected, NotInProfile, NoDdc, NotWritable, NotConfirmed, Failed }

/// <summary>İsteğe bağlı akşam geçişi. Varsayılan kapalı; sadece renk değişir, arka ışık değişmez.</summary>
/// <param name="Start">Yerel saat.</param>
/// <param name="DurationMinutes">30–60.</param>
public sealed record EveningSettings(bool Enabled, TimeOnly Start, int DurationMinutes, int TargetKelvin)
{
    public static EveningSettings Default { get; } = new(false, new TimeOnly(21, 0), 45, 3400);

    public EveningSettings Normalize() => this with
    {
        DurationMinutes = Math.Clamp(DurationMinutes, EveningTransition.MinDurationMinutes, EveningTransition.MaxDurationMinutes),
        TargetKelvin = Math.Clamp(TargetKelvin, KelvinCalculator.MinKelvin, KelvinCalculator.OffKelvin),
    };
}

/// <param name="Applied">Donanıma yazma (DDC ve GPU tablosu) tamamlandı; askıdaysa false.</param>
/// <param name="Monitor">Şu an monitörün RGB kazancında olan pay (doğrusal ışık).</param>
/// <param name="Gpu">GPU katmanındaki pay.</param>
/// <param name="Conflicts">Filtre açıkken çalışan Gece ışığı / f.lux; kullanıcıya uyarı gösterilir.</param>
public sealed record BlueLightState(
    int Kelvin, bool Applied, RgbGains Monitor, RgbGains Gpu, IReadOnlyList<ColorConflict> Conflicts);

/// <summary>
/// Mavi ışık filtresi (T307). 6500 = kapalı. Önce monitörün RGB kazancı, sınırda GPU katmanı; kalibrasyonun
/// üstüne fark olarak uygulanır. Tek iş parçacığından çağrılır (arayüz).
/// </summary>
public interface IBlueLightFilter
{
    int Kelvin { get; }

    /// <summary>Ayar sihirbazı ve DaVinci profili sırasında filtre geçici kapalıdır.</summary>
    bool IsSuspended { get; }

    EveningSettings Evening { get; set; }

    /// <summary>Kullanıcı seçimi (kaydırıcı bırakılınca). Akşam geçişini o akşam için durdurur.</summary>
    BlueLightState SetKelvin(int kelvin);

    BlueLightState Disable();

    /// <summary>Kazançları eski değerine döndürür ve GPU katmanını kapatır; Kelvin seçimi saklanır.</summary>
    BlueLightState Suspend();

    BlueLightState Resume();

    /// <summary>Uygulama kapanırken çağrılır: kazançlar eski değerine döner, GPU katmanı kapanır (Suspend gibi).</summary>
    BlueLightState Shutdown();

    /// <summary>Monitörün kendi Low Blue Light ön ayarı. Önce filtre kapanır.</summary>
    LowBlueLightResult SelectMonitorLowBlueLight();

    IReadOnlyList<ColorConflict> CheckConflicts();

    /// <summary>Zamanlayıcı çağırır. Akşam geçişinde sıradaki adım gelmişse uygular; true: renk değişti.</summary>
    bool Tick();
}

public sealed class BlueLightFilter : IBlueLightFilter
{
    /// <summary>Monitör kazancı kodunun inebileceği en düşük oran (baz değere göre; kodlanmış değerde). Ötesi GPU'da.</summary>
    public const double MonitorMinimumCode = 0.6;

    /// <summary>Geçiş bittikten sonra uygulama geç açılırsa hedefe geçilebilecek süre; sabaha kadar uzamaz.</summary>
    public static readonly TimeSpan EveningHold = TimeSpan.FromHours(6);

    private const byte ColorPresetCode = 0x14;
    private static readonly byte[] GainCodes = [0x16, 0x18, 0x1A]; // R, G, B
    private static readonly double MonitorFloor = Math.Pow(MonitorMinimumCode, CalibrationChoices.TargetGamma);

    private readonly MonitorProfile _profile;
    private readonly IDdcControl _ddc;
    private readonly IColorPipeline _pipeline;
    private readonly IKelvinCalculator _calculator;
    private readonly IColorConflictProbe _probe;
    private readonly TimeProvider _time;
    private readonly ISettingsStore _store;
    private readonly MonitorKey _key;

    private EveningSettings _evening = EveningSettings.Default;
    private int _kelvin = KelvinCalculator.OffKelvin;
    private bool _suspended;
    private bool _layerActive;
    public const string BaseSection = "BlueLightBase";

    private bool _persistedLoaded;
    private ushort[]? _baseCodes;               // filtre açılmadan önceki kazançlar (kalibre değerler); depoda da tutulur
    private ushort[] _currentCodes = [];        // monitörde şu an bizim yazdığımız kodlar
    private readonly double[] _monitorShare = [1, 1, 1]; // monitörde etkili pay (doğrusal ışık)
    private DateTime? _manualEpisode;

    public BlueLightFilter(
        MonitorProfile profile, IDdcControl ddc, IColorPipeline pipeline, IKelvinCalculator calculator,
        IColorConflictProbe probe, TimeProvider time, ISettingsStore store, MonitorKey key)
    {
        _store = store;
        _key = key;
        _profile = profile;
        _ddc = ddc;
        _pipeline = pipeline;
        _calculator = calculator;
        _probe = probe;
        _time = time;
    }

    public int Kelvin => _kelvin;

    public bool IsSuspended => _suspended;

    public EveningSettings Evening
    {
        get => _evening;
        set => _evening = value.Normalize();
    }

    public BlueLightState SetKelvin(int kelvin)
    {
        MarkManual();
        _kelvin = Math.Clamp(kelvin, KelvinCalculator.MinKelvin, KelvinCalculator.OffKelvin);
        return _suspended ? State(applied: false) : Apply();
    }

    public BlueLightState Disable() => SetKelvin(KelvinCalculator.OffKelvin);

    public BlueLightState Suspend()
    {
        _suspended = true;
        return Restore();
    }

    public BlueLightState Resume()
    {
        _suspended = false;
        return Apply();
    }

    public BlueLightState Shutdown() => Suspend();

    public IReadOnlyList<ColorConflict> CheckConflicts() => _probe.Detect();

    public LowBlueLightResult SelectMonitorLowBlueLight()
    {
        var mapping = _profile.ColorPresetMap.FirstOrDefault(m => m.Preset == OsdColorPreset.LowBlueLight);
        if (mapping is null) return LowBlueLightResult.NotInProfile;
        if (!_ddc.IsAvailable) return LowBlueLightResult.NoDdc;
        if (!_ddc.WritableCodes.Contains(ColorPresetCode)) return LowBlueLightResult.NotWritable;
        // Profil kuralı: monitörde doğrulanmamış (estimated) eşleme yazılmaz.
        if (mapping.Confidence != MappingConfidence.Confirmed) return LowBlueLightResult.NotConfirmed;

        Disable();
        return _ddc.Write(ColorPresetCode, (ushort)mapping.DdcValue) == DdcWriteResult.Written
            ? LowBlueLightResult.Selected
            : LowBlueLightResult.Failed;
    }

    public bool Tick()
    {
        if (!_evening.Enabled || _suspended) return false;

        var now = _time.GetLocalNow().DateTime;
        var episode = CurrentEpisode(now);
        var duration = TimeSpan.FromMinutes(_evening.DurationMinutes);
        var elapsed = now - episode;
        if (elapsed >= duration + EveningHold || _manualEpisode == episode) return false;

        // Sadece ısıtır: kullanıcının seçtiğinden daha soğuğa (yukarı) çekmez.
        var wanted = EveningTransition.KelvinAt(elapsed, duration, _evening.TargetKelvin);
        if (wanted >= _kelvin) return false;

        _kelvin = wanted;
        Apply();
        return true;
    }

    // ---- Uygulama ----

    private BlueLightState Apply()
    {
        if (_kelvin >= KelvinCalculator.OffKelvin) return Restore();

        var monitorUsable = TryUseMonitorGain();
        var floor = monitorUsable ? new RgbGains(MonitorFloor, MonitorFloor, MonitorFloor) : RgbGains.Neutral;
        var split = _calculator.Calculate(_kelvin, floor);
        if (monitorUsable) WriteMonitor(split.Monitor);
        else Array.Fill(_monitorShare, 1.0); // monitör payı etkisiz (başka ön ayar, DDC yok): hepsi GPU'da

        // GPU, hedefin monitörde gerçekten uygulanamayan kısmını alır (yazma sınırlandıysa payı büyür).
        var gpu = new RgbGains(
            GpuShare(split.Monitor.R * split.Gpu.R, _monitorShare[0]),
            GpuShare(split.Monitor.G * split.Gpu.G, _monitorShare[1]),
            GpuShare(split.Monitor.B * split.Gpu.B, _monitorShare[2]));

        var applied = true;
        if (gpu != RgbGains.Neutral || _layerActive)
        {
            _pipeline.SetLayer(ColorLayer.BlueLight, gpu == RgbGains.Neutral ? null : GammaRamps.Scale(gpu));
            _layerActive = gpu != RgbGains.Neutral;
            applied = _pipeline.Apply();
        }

        return new BlueLightState(
            _kelvin, applied && AllMonitorWritesDone(split.Monitor), MonitorShare(), gpu,
            _probe.Detect());
    }

    /// <summary>Filtre kapalı ya da askıda: kazançlar eski değere, GPU katmanı kapalı.</summary>
    private BlueLightState Restore()
    {
        var restored = true;
        LoadPersisted();
        SyncExternal();
        if (_baseCodes is not null)
        {
            for (var i = 0; i < GainCodes.Length; i++)
            {
                if (_currentCodes[i] != _baseCodes[i]
                    && _ddc.Write(GainCodes[i], _baseCodes[i]) != DdcWriteResult.Written)
                {
                    restored = false;
                    continue;
                }

                _currentCodes[i] = _baseCodes[i];
                _monitorShare[i] = 1;
            }

            if (restored)
            {
                _baseCodes = null;
                _store.Write(_key, BaseSection, new BlueLightBaseRecord([], []));
            }
            else SavePersisted();
        }

        var applied = restored;
        if (_layerActive)
        {
            _pipeline.SetLayer(ColorLayer.BlueLight, null);
            _layerActive = false;
            applied &= _pipeline.Apply();
        }

        return new BlueLightState(_kelvin, applied, MonitorShare(), RgbGains.Neutral, []);
    }

    private BlueLightState State(bool applied) =>
        new(_kelvin, applied, MonitorShare(), RgbGains.Neutral, []);

    /// <summary>
    /// Monitör payı kullanılabilir mi: DDC var, 16/18/1A yazılabilir ve renk ön ayarı RGB kazancının etkili olduğu
    /// ön ayar (profil). İlk kullanımda mevcut kazançlar baz olarak alınır.
    /// </summary>
    private bool TryUseMonitorGain()
    {
        if (!_ddc.IsAvailable || !GainCodes.All(_ddc.WritableCodes.Contains)) return false;

        if (_ddc.Read(ColorPresetCode) is not { } preset) return false;
        var mapping = _profile.ColorPresetMap.FirstOrDefault(m => m.DdcValue == preset.Current);
        if (mapping?.Preset != _profile.Osd.RgbGainPreset) return false;

        LoadPersisted();
        if (_baseCodes is not null)
        {
            SyncExternal();
            return true;
        }

        var values = GainCodes.Select(c => _ddc.Read(c)).ToArray();
        if (values.Any(v => v is null or { Current: 0 })) return false;
        _baseCodes = [.. values.Select(v => v!.Value.Current)];
        _currentCodes = [.. _baseCodes];
        Array.Fill(_monitorShare, 1.0);
        SavePersisted();
        return true;
    }

    /// <summary>Önceki oturumdan (çökme, kapanış) kalan baz varsa onu alır; bir kez bakar.</summary>
    private void LoadPersisted()
    {
        if (_persistedLoaded) return;
        _persistedLoaded = true;
        if (_store.Read<BlueLightBaseRecord>(_key, BaseSection) is not { Base.Length: 3, Applied.Length: 3 } p
            || p.Base.Contains((ushort)0)) return;

        _baseCodes = [.. p.Base];
        _currentCodes = [.. p.Applied];
    }

    private void SavePersisted()
    {
        if (_baseCodes is not null)
            _store.Write(_key, BaseSection, new BlueLightBaseRecord([.. _baseCodes], [.. _currentCodes]));
    }

    /// <summary>
    /// 16/18/1A bizim yazdığımızdan farklıysa kullanıcı (OSD, T305) değiştirmiştir: o kanalda yeni baz odur
    /// (02-MIMARI.md → Ayar sahipliği 1), geri yükleme onu ezmez.
    /// </summary>
    private void SyncExternal()
    {
        if (_baseCodes is null) return;
        var changed = false;
        for (var i = 0; i < GainCodes.Length; i++)
        {
            if (_ddc.Read(GainCodes[i]) is not { Current: > 0 } actual || actual.Current == _currentCodes[i]) continue;
            _baseCodes[i] = actual.Current;
            _currentCodes[i] = actual.Current;
            _monitorShare[i] = 1;
            changed = true;
        }

        if (changed) SavePersisted();
    }

    /// <summary>Kodlar bazın oranıdır ve sadece azalır (255 = tam); aynı değer tekrar yazılmaz.</summary>
    private void WriteMonitor(RgbGains intended)
    {
        double[] shares = [intended.R, intended.G, intended.B];
        var wrote = false;
        for (var i = 0; i < GainCodes.Length; i++)
        {
            var code = (ushort)Math.Min(_baseCodes![i],
                Math.Round(_baseCodes[i] * GammaRamps.EncodedGain(shares[i]), MidpointRounding.AwayFromZero));
            if (code != _currentCodes[i])
            {
                if (_ddc.Write(GainCodes[i], code) != DdcWriteResult.Written) continue;
                _currentCodes[i] = code;
                wrote = true;
            }

            _monitorShare[i] = shares[i];
        }

        if (wrote) SavePersisted();
    }

    private bool AllMonitorWritesDone(RgbGains intended) =>
        _monitorShare[0] == intended.R && _monitorShare[1] == intended.G && _monitorShare[2] == intended.B;

    private static double GpuShare(double target, double monitorShare)
    {
        var share = Math.Clamp(target / monitorShare, 0, 1);
        return share >= 1 - 1e-6 ? 1 : share;
    }

    private RgbGains MonitorShare() => new(_monitorShare[0], _monitorShare[1], _monitorShare[2]);

    // ---- Akşam geçişi: kullanıcının elle değiştirdiği akşam otomatik dokunulmaz ----

    private void MarkManual() => _manualEpisode = CurrentEpisode(_time.GetLocalNow().DateTime);

    private DateTime CurrentEpisode(DateTime now)
    {
        var start = now.Date + _evening.Start.ToTimeSpan();
        return now < start ? start.AddDays(-1) : start;
    }
}
