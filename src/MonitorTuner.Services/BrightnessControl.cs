using MonitorTuner.Color;
using MonitorTuner.Core;

namespace MonitorTuner.Services;

/// <param name="Plan">Uygulanmak istenen plan (arka ışık 0–100, GPU beyaz seviyesi; taban uygulanmış).</param>
/// <param name="Applied">Hem arka ışık hem GPU yazması tamamlandı.</param>
/// <param name="BacklightWrite">DDC 10 yazma sonucu; yazma gerekmediyse null.</param>
public sealed record BrightnessState(DimmingPlan Plan, bool Applied, DdcWriteResult? BacklightWrite);

/// <summary>
/// Parlaklık ve ek karartma (T306): tek kaydırıcı. Arka ışık (DDC 10) 100 → 0 iner, sonra GPU ile ek karartma
/// (beyaz en az %15). Parlaklık sadece kullanıcı <see cref="Commit"/> edince değişir; Hz'e dokunulmaz.
/// Tek iş parçacığından çağrılır (arayüz).
/// </summary>
public interface IBrightnessControl
{
    /// <summary>GPU'da ek karartma katmanı açık mı.</summary>
    bool DimmingActive { get; }

    /// <summary>Sürüklerken: planı hesaplar, donanıma hiç dokunmaz.</summary>
    DimmingPlan Preview(double slider);

    /// <summary>Kaydırıcı bırakılınca: arka ışığa tek yazma, gerekiyorsa GPU katmanı. Değer monitörde zaten oradaysa bellek yazılmaz (DdcControl).</summary>
    BrightnessState Commit(double slider);

    /// <summary>Açılışta kaydırıcının yeri (arka ışığı okur, yazmaz). null: DDC yok ya da okunamadı.</summary>
    double? ReadSlider();

    /// <summary>
    /// Panik kısayolu: ek karartma katmanını kapatır (arka ışık kalır). Kullanıcı kaydırıcıyı yeniden bırakana kadar kapalı.
    /// Katmanı kapatmanın tek yeri; true: tablo yazıldı.
    /// </summary>
    bool DisableDimming();

    /// <summary>Ayar sihirbazı sırasında: katman kapanır, kaydırıcı saklanır. Yeni Commit arka ışığı yazar ama katmanı açmaz.</summary>
    bool Suspend();

    /// <summary>Askıdan dönüş: saklanan kaydırıcının katmanı geri gelir (panikle kapatıldıysa gelmez).</summary>
    bool Resume();

    /// <summary>Uygulama kapanırken: katman kapanır (<see cref="Suspend"/> gibi), arka ışığa dokunulmaz.</summary>
    bool Shutdown();
}

public sealed class BrightnessControl(IDdcControl ddc, IColorPipeline pipeline, IDimmingPlanner planner) : IBrightnessControl
{
    private const byte BacklightCode = 0x10;
    private const int DefaultMaximum = 100;

    private double? _slider;
    private double _layerScale = 1; // GPU Dimming katmanında şu an olan beyaz seviyesi; 1 = katman kapalı
    private bool _suspended;
    private bool _panicOff;

    public bool DimmingActive => _layerScale < 1;

    public DimmingPlan Preview(double slider) => Safe(planner.Plan(slider));

    public BrightnessState Commit(double slider)
    {
        var plan = Safe(planner.Plan(slider));
        _slider = Math.Clamp(slider, 0.0, 1.0);
        _panicOff = false; // kullanıcı kaydırıcıya dokundu: ek karartma yeniden serbest

        var wantedScale = WantedScale(plan);
        var gpuFirst = wantedScale < _layerScale; // karartırken önce GPU, açarken önce arka ışık: ara durum hedeflerden parlak olmaz
        var applied = true;
        DdcWriteResult? backlight = null;

        if (gpuFirst) applied &= SetLayer(wantedScale);
        // Her Commit'te yazılır: önbellek OSD / DdcPanel / sihirbazın değiştirdiği değeri göremez. Değer yerindeyse
        // DdcControl.Write belleğe yazmadan döner.
        backlight = WriteBacklight(plan.Backlight);
        applied &= backlight == DdcWriteResult.Written;

        // Geçici DDC hatasında (sınır, başarısız) GPU katmanı kalkmaz: kullanıcı karanlıkta kalır, parlak anlık görmez.
        if (!gpuFirst && backlight is not (DdcWriteResult.Throttled or DdcWriteResult.Failed))
            applied &= SetLayer(wantedScale);
        else if (!gpuFirst) applied = false;

        return new BrightnessState(plan, applied, backlight);
    }

    public double? ReadSlider()
    {
        if (!ddc.IsAvailable || ddc.Read(BacklightCode) is not { } value) return null;

        var maximum = value.Maximum == 0 ? DefaultMaximum : value.Maximum;
        var backlight = (int)Math.Round((double)value.Current / maximum * DimmingPlanner.MaxBacklight, MidpointRounding.AwayFromZero);
        // Arka ışık 0 iken ek karartma açıksa kaydırıcı DDC'den çıkarılamaz: son bırakılan yer.
        return DimmingActive && backlight == 0 && _slider is { } s ? s : DimmingPlanner.SliderForBacklight(backlight);
    }

    public bool DisableDimming()
    {
        _panicOff = true;
        return SetLayer(1);
    }

    public bool Suspend()
    {
        _suspended = true;
        return SetLayer(1);
    }

    public bool Resume()
    {
        _suspended = false;
        return _slider is { } s ? SetLayer(WantedScale(Safe(planner.Plan(s)))) : true;
    }

    public bool Shutdown() => Suspend();

    // ---- İç ----

    /// <summary>Panik ya da askıdayken katman kapalıdır; değilse planın ölçeği.</summary>
    private double WantedScale(DimmingPlan plan) => _suspended || _panicOff ? 1 : plan.GpuScale;

    /// <summary>Planlayıcı ne verirse versin: taban altına inilmez, arka ışık 0–100'de kalır (son savunma).</summary>
    private static DimmingPlan Safe(DimmingPlan plan) => new(
        Math.Clamp(plan.Backlight, 0, DimmingPlanner.MaxBacklight),
        Math.Clamp(plan.GpuScale, SafetyLimits.MinWhiteLevel, 1.0));

    /// <summary>Dimming katmanını yazar (GPU'ya sadece renk boru hattı). Aynı ölçek tekrar yazılmaz.</summary>
    private bool SetLayer(double scale)
    {
        if (scale == _layerScale) return true;

        pipeline.SetLayer(ColorLayer.Dimming, LayerTable(scale));
        if (!pipeline.Apply())
        {
            // Reddedilen tablo boru hattında kalmasın: sonraki Apply (ör. mavi ışık) onu yazmaya çalışmasın.
            pipeline.SetLayer(ColorLayer.Dimming, LayerTable(_layerScale));
            return false;
        }

        _layerScale = scale;
        return true;
    }

    private static GammaRamp? LayerTable(double scale) =>
        scale >= 1 ? null : GammaRamps.Scale(new RgbGains(scale, scale, scale));

    private DdcWriteResult WriteBacklight(int percent)
    {
        if (!ddc.IsAvailable) return DdcWriteResult.NoDdc;
        if (!ddc.WritableCodes.Contains(BacklightCode)) return DdcWriteResult.Forbidden;

        var maximum = ddc.Read(BacklightCode) is { Maximum: > 0 } v ? v.Maximum : DefaultMaximum;
        var code = (ushort)Math.Round((double)percent / DimmingPlanner.MaxBacklight * maximum, MidpointRounding.AwayFromZero);
        return ddc.Write(BacklightCode, code);
    }
}
