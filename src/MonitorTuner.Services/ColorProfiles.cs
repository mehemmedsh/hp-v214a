using MonitorTuner.Color;
using MonitorTuner.Core;

namespace MonitorTuner.Services;

/// <summary>Bir ayarın profil uygulanırken durumu.</summary>
/// <remarks>
/// <see cref="Skipped"/>: profil o ayara dokunmuyor. <see cref="Deferred"/>: ayar sihirbazı sırasında mavi ışık askıda;
/// seçim saklandı, sihirbaz bitince gelir. <see cref="Failed"/>: yazılamadı (ör. DDC yok ya da yazma sınırı, tablo reddedildi,
/// sürücü cevap vermedi); diğer ayarlar yine de uygulanır.
/// </remarks>
public enum ProfileStep { Skipped, Applied, Deferred, Failed }

/// <summary>Profil uygulamanın sonucu: ayar başına durum.</summary>
public sealed record ProfileApplyResult(string Name, ProfileStep Brightness, ProfileStep BlueLight, ProfileStep Vibrance)
{
    /// <summary>Hiçbir ayar başarısız olmadı (atlananlar ve ertelenenler başarısızlık sayılmaz).</summary>
    public bool Success => Brightness != ProfileStep.Failed && BlueLight != ProfileStep.Failed && Vibrance != ProfileStep.Failed;
}

/// <summary>Kullanıcı profilleri (ayar deposu, "ColorProfiles" bölümü). Hazır profiller depoda tutulmaz.</summary>
public sealed record ColorProfilesRecord(ColorProfile[] Profiles);

/// <summary>
/// Renk profilleri (T308). Sadece <see cref="Apply"/> çağrılınca çalışır: kendiliğinden uygulanmaz, arkada zamanlayıcısı
/// yoktur. Parlaklığı <see cref="IBrightnessControl"/>, mavi ışığı <see cref="IBlueLightFilter"/>, canlılığı
/// <see cref="INvidiaControl"/> üzerinden uygular; yazma sınırı, Güvenlik ve geri alma noktaları onlardadır. Hz'e
/// (ekran modları) ve kalibrasyona ulaşan bir bağımlılığı yoktur. Tek iş parçacığından çağrılır (arayüz).
/// </summary>
public sealed class ColorProfiles : IColorProfiles
{
    public const string Section = "ColorProfiles";
    public const int MaxNameLength = 40;

    /// <summary>NVIDIA panelinde %50 varsayılandır ve altı yazılmaz (<c>INvidiaControl.SetVibrance</c>).</summary>
    public const int MinVibrance = 50, MaxVibrance = 100;

    /// <summary>
    /// Hazır profiller, bu sırayla. Parlaklık kaydırıcı değeridir (0,2 üstü: sadece arka ışık, ek karartma yok); 0,6 ≈ arka ışık
    /// %50 (masaüstü 100–120 nit hedefi). Canlılık sadece Film ve Oyun'da; DaVinci zaman çizelgesi nötr ve efektsizdir
    /// (mavi ışık kapalı, canlılık yok, ek karartma yok).
    /// </summary>
    public static IReadOnlyList<ColorProfile> BuiltIns { get; } =
    [
        new("Gündüz", true, 0.60, KelvinCalculator.OffKelvin, null),
        new("Akşam", true, 0.50, 3400, null),
        new("Gece", true, 0.30, 2700, null), // mavi ışık + düşük parlaklık (08-GORUNTU-YONTEMLERI.md → 4)
        new("Kod", true, 0.55, 5000, null),
        new("Film", true, 0.50, KelvinCalculator.OffKelvin, 60),
        new("Oyun", true, 0.65, KelvinCalculator.OffKelvin, 65),
        new("DaVinci zaman çizelgesi", true, 0.60, KelvinCalculator.OffKelvin, null),
    ];

    private readonly IBrightnessControl _brightness;
    private readonly IBlueLightFilter _blueLight;
    private readonly INvidiaControl _nvidia;
    private readonly ISettingsStore _store;
    private readonly MonitorKey _key;

    private List<ColorProfile>? _user;

    public ColorProfiles(
        IBrightnessControl brightness, IBlueLightFilter blueLight, INvidiaControl nvidia, ISettingsStore store, MonitorKey key)
    {
        _brightness = brightness;
        _blueLight = blueLight;
        _nvidia = nvidia;
        _store = store;
        _key = key;
    }

    public IReadOnlyList<ColorProfile> Profiles => [.. BuiltIns, .. User()];

    public ProfileApplyResult Apply(ColorProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Validate(profile); // geçersiz profil hiçbir şey yazmaz

        var brightness = profile.Brightness is { } slider
            ? Step(_brightness.Commit(slider).Applied)
            : ProfileStep.Skipped;

        var blueLight = ProfileStep.Skipped;
        if (profile.Kelvin is { } kelvin)
        {
            var state = _blueLight.SetKelvin(kelvin);
            blueLight = state.Applied ? ProfileStep.Applied : _blueLight.IsSuspended ? ProfileStep.Deferred : ProfileStep.Failed;
        }

        // Canlılığı olmayan profil, uygulamanın açtığı canlılığı kapatır; kullanıcının kendi değerine dokunmaz.
        var vibrance = Step(profile.Vibrance is { } v ? _nvidia.SetVibrance(v) : _nvidia.ResetVibrance());

        return new ProfileApplyResult(profile.Name, brightness, blueLight, vibrance);
    }

    public void Save(ColorProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var saved = profile with { Name = profile.Name?.Trim()!, BuiltIn = false };
        Validate(saved);
        if (IsBuiltInName(saved.Name)) throw new ArgumentException($"'{saved.Name}' hazır bir profilin adı.", nameof(profile));

        var user = User();
        var index = user.FindIndex(p => SameName(p.Name, saved.Name));
        if (index >= 0) user[index] = saved;
        else user.Add(saved);
        Persist();
    }

    public void Delete(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (IsBuiltInName(name.Trim())) throw new InvalidOperationException($"'{name}' hazır bir profil; silinemez.");

        if (User().RemoveAll(p => SameName(p.Name, name.Trim())) > 0) Persist();
    }

    /// <summary>
    /// Panik kısayolu (T310 bağlar): ek karartma, mavi ışık ve canlılık kapanır. Kalibrasyon katmanına, Hz'e ve arka ışığa
    /// dokunulmaz. Biri atarsa ötekiler yine de kapanır.
    /// </summary>
    public void PanicOff()
    {
        try { _brightness.DisableDimming(); }
        finally
        {
            try { _blueLight.Disable(); }
            finally { _nvidia.ResetVibrance(); }
        }
    }

    /// <summary>Profili doğrular (ad ve değer aralıkları); geçersizse <see cref="ArgumentException"/>.</summary>
    public static void Validate(ColorProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var name = profile.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength)
            throw new ArgumentException($"Profil adı 1–{MaxNameLength} karakter olmalı.", nameof(profile));
        // Sayı değilse (NaN) ya da aralık dışındaysa reddedilir: karşılaştırmalar NaN'da false döner.
        if (profile.Brightness is { } b && !(b is >= 0.0 and <= 1.0))
            throw new ArgumentException("Parlaklık 0–1 olmalı.", nameof(profile));
        if (profile.Kelvin is { } k && k is < KelvinCalculator.MinKelvin or > KelvinCalculator.OffKelvin)
            throw new ArgumentException($"Kelvin {KelvinCalculator.MinKelvin}–{KelvinCalculator.OffKelvin} olmalı.", nameof(profile));
        if (profile.Vibrance is { } v && v is < MinVibrance or > MaxVibrance)
            throw new ArgumentException($"Canlılık {MinVibrance}–{MaxVibrance} olmalı.", nameof(profile));
    }

    // ---- İç ----

    private static ProfileStep Step(bool ok) => ok ? ProfileStep.Applied : ProfileStep.Failed;

    private static bool SameName(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool IsBuiltInName(string name) => BuiltIns.Any(p => SameName(p.Name, name));

    /// <summary>
    /// Kullanıcı profilleri, ilk kullanımda depodan. Hazır bir adla kayıt (hazır olan kazanır), geçersiz değerli ve aynı adlı
    /// ikinci kayıt (elle bozulmuş dosya) atılır; böyle bir profil hiç uygulanamaz.
    /// </summary>
    private List<ColorProfile> User()
    {
        if (_user is not null) return _user;

        _user = [];
        foreach (var stored in _store.Read<ColorProfilesRecord>(_key, Section)?.Profiles ?? [])
        {
            try
            {
                if (stored is null) continue;
                var profile = stored with { Name = stored.Name?.Trim()!, BuiltIn = false };
                Validate(profile);
                if (!IsBuiltInName(profile.Name) && !_user.Any(p => SameName(p.Name, profile.Name))) _user.Add(profile);
            }
            catch (ArgumentException)
            {
                // geçersiz kayıt listeden düşer
            }
        }

        return _user;
    }

    private void Persist() => _store.Write(_key, Section, new ColorProfilesRecord([.. User()]));
}
