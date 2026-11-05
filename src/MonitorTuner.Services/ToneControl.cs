using MonitorTuner.Color;

namespace MonitorTuner.Services;

/// <summary>
/// Ton eğrisi (T320): sinema gamması (2,4) ya da canlı kontrast. Bir görüntü efektidir: renk profili seçince kurulur, panik
/// kısayoluyla kapanır, ayar sihirbazı sırasında askıya alınır, uygulama kapanınca tablodan kalkar (renk boru hattının
/// <c>Shutdown</c>'u sadece kalibrasyonu bırakır). Parlaklığa, Hz'e ve DDC'ye dokunmaz. Tek iş parçacığından çağrılır (arayüz).
/// </summary>
public interface IToneControl
{
    /// <summary>Kullanıcının seçtiği ton; askıdayken de seçim saklanır.</summary>
    ToneStyle Style { get; }

    /// <summary>Ayar sihirbazı sırasında ton katmanı kapalıdır (gamma ham panelde ölçülür).</summary>
    bool IsSuspended { get; }

    /// <summary>
    /// Seçim: ton katmanını kurar ve tabloyu yazar. true: ekranda. false: askıda (seçim saklandı, sihirbaz bitince gelir) ya da
    /// Windows tabloyu reddetti (ekranda ve <see cref="Style"/>'da önceki ton kalır). Bilinmeyen stil <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    bool Set(ToneStyle style);

    /// <summary>Panik: ton katmanı kapanır (Standart). Askıdayken çağrılırsa sihirbaz bitince de geri gelmez.</summary>
    bool Disable();

    /// <summary>Ayar sihirbazı başlarken: katman kalkar, seçim saklanır.</summary>
    bool Suspend();

    /// <summary>Askıdan dönüş: saklanan ton geri gelir.</summary>
    bool Resume();
}

/// <summary>Ton katmanının sahibi (T320). GPU tablosuna yalnız <see cref="IColorPipeline"/> üzerinden ulaşır.</summary>
public sealed class ToneControl(IColorPipeline pipeline, IToneCurves curves) : IToneControl
{
    private ToneStyle _style = ToneStyle.Standard;   // kullanıcının seçimi
    private ToneStyle _onLayer = ToneStyle.Standard; // boru hattının ton katmanında şu an olan
    private bool _suspended;

    public ToneStyle Style => _style;

    public bool IsSuspended => _suspended;

    public bool Set(ToneStyle style)
    {
        if (!Enum.IsDefined(style)) throw new ArgumentOutOfRangeException(nameof(style), style, "Bilinmeyen ton stili.");
        if (_suspended)
        {
            _style = style; // sihirbaz bitince gelir
            return false;
        }

        if (!Show(style)) return false; // ekranda ve seçimde önceki ton kalır
        _style = style;
        return true;
    }

    public bool Disable()
    {
        _style = ToneStyle.Standard;
        return _suspended || TakeOff();
    }

    public bool Suspend()
    {
        if (_suspended) return true;
        _suspended = true;
        return TakeOff();
    }

    public bool Resume()
    {
        if (!_suspended) return true;
        _suspended = false;
        if (Show(_style)) return true;
        _style = _onLayer; // reddedildi: seçim ekrandakiyle aynı kalsın
        return false;
    }

    /// <summary>
    /// Katmanı kurar ve yazar. Windows reddederse katman eski hâline döner (yazılmadan): reddedilen tablo katmanda kalsaydı
    /// mavi ışık ya da karartmanın sonraki her yazması da reddedilirdi.
    /// </summary>
    private bool Show(ToneStyle style)
    {
        pipeline.SetLayer(ColorLayer.Tone, curves.Build(style));
        if (pipeline.Apply())
        {
            _onLayer = style;
            return true;
        }

        pipeline.SetLayer(ColorLayer.Tone, curves.Build(_onLayer));
        return false;
    }

    /// <summary>
    /// Katmanı kaldırır (panik, sihirbaz). Yazma başarısız olsa da katman boş kalır: ekrandaki tablo bir sonraki başarılı
    /// yazmada tonsuz olur; sihirbazın ölçümü ve panik ton eğrisini geri getirmez.
    /// </summary>
    private bool TakeOff()
    {
        pipeline.SetLayer(ColorLayer.Tone, null);
        _onLayer = ToneStyle.Standard;
        return pipeline.Apply();
    }
}
