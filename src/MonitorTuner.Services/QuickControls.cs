using MonitorTuner.Color;

namespace MonitorTuner.Services;

/// <summary>Hızlı denetimin sonucu. <see cref="Blocked"/>: ayar sihirbazı açık, hiçbir şey yazılmadı.</summary>
public enum QuickOutcome { Done, Partial, Failed, Blocked, NotFound }

/// <param name="Profile">Profil seçildiyse ayar başına durum (hangi ayar uygulanamadı, hangisi ertelendi).</param>
/// <param name="Error">Panik sırasında bir servis hata attıysa iletisi.</param>
public sealed record QuickResult(QuickOutcome Outcome, ProfileApplyResult? Profile = null, string? Error = null);

/// <summary>
/// Ekranda olan mod (T322): son seçilen profil (servisteki sabit adıyla). <paramref name="Adjusted"/>: seçimden sonra parlaklık
/// ya da mavi ışık elle değiştirildi, ekranda artık tam o mod yok.
/// </summary>
public sealed record ActiveMode(string Name, bool Adjusted);

/// <summary>
/// Hızlı denetimler (T310): sistem tepsisinin ve ana penceredeki profil seçiminin ortak girişi. Arayüz donanıma ve renk
/// servislerine doğrudan değil, buradan ulaşır; böylece "sihirbaz açıkken kapalı" kuralı tek yerde durur.
/// </summary>
public interface IQuickControls
{
    /// <summary>
    /// false: ayar sihirbazı açık. Profil, parlaklık ve mavi ışık seçimi o sürece kapalıdır (sihirbaz mavi ışığı, ek karartmayı
    /// ve canlılığı askıya almıştır; arka ışığı da kendisi yazar). Arayüz bu değere bakıp ögeleri kapatır. Panik hep açıktır.
    /// </summary>
    bool Enabled { get; }

    /// <summary>Hazır ve kullanıcı profilleri.</summary>
    IReadOnlyList<ColorProfile> Profiles { get; }

    /// <summary>
    /// Ekranda olan mod (T322); null: henüz seçilmedi ya da panikle kapandı. Sadece okunur, hiçbir şey yazmaz. Uygulama yeniden
    /// açılınca efektler kapalı başladığı için null'dur.
    /// </summary>
    ActiveMode? Active { get; }

    QuickResult SelectProfile(string name);

    /// <summary>Tek kaydırıcı (0–1), <see cref="IBrightnessControl.Commit"/>.</summary>
    QuickResult SetBrightness(double slider);

    /// <summary>Mavi ışık (<see cref="KelvinCalculator.MinKelvin"/>–<see cref="KelvinCalculator.OffKelvin"/>; 6500 = kapalı).</summary>
    QuickResult SetBlueLight(int kelvin);

    /// <summary>Panik kısayolu: mavi ışık, ek karartma, ton eğrisi ve canlılık kapanır; kalibrasyon, arka ışık ve Hz kalır.</summary>
    QuickResult Panic();
}

/// <summary>
/// Tek iş parçacığından çağrılır (arayüz). Yalnız profiller, parlaklık, mavi ışık ve sihirbaz durumunu görür: Hz (ekran
/// modları), Güvenlik ve renk boru hattı (kalibrasyon) bağımlılığı yoktur. Kendiliğinden hiçbir şey yazmaz.
/// </summary>
public sealed class QuickControls(
    IColorProfiles profiles, IBrightnessControl brightness, IBlueLightFilter blueLight, ISetupWizard wizard) : IQuickControls
{
    private static readonly QuickResult BlockedResult = new(QuickOutcome.Blocked);

    private ActiveMode? _active;

    public bool Enabled => !wizard.IsActive;

    public IReadOnlyList<ColorProfile> Profiles => profiles.Profiles;

    public ActiveMode? Active => _active;

    public QuickResult SelectProfile(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!Enabled) return BlockedResult;

        var profile = profiles.Profiles.FirstOrDefault(p => string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (profile is null) return new QuickResult(QuickOutcome.NotFound);

        var result = profiles.Apply(profile);
        _active = new ActiveMode(profile.Name, Adjusted: false); // kısmen de olsa ekrandaki mod bu; eksikleri sonuç söyler
        // Ertelenen adım ekranda henüz yok; tam başarı sayılmaz.
        return new QuickResult(result.Complete ? QuickOutcome.Done : QuickOutcome.Partial, result);
    }

    public QuickResult SetBrightness(double slider)
    {
        if (!(slider is >= 0.0 and <= 1.0)) throw new ArgumentOutOfRangeException(nameof(slider), slider, "Parlaklık 0–1 olmalı.");
        if (!Enabled) return BlockedResult;

        return Manual(brightness.Commit(slider).Applied);
    }

    public QuickResult SetBlueLight(int kelvin)
    {
        if (kelvin is < KelvinCalculator.MinKelvin or > KelvinCalculator.OffKelvin)
            throw new ArgumentOutOfRangeException(nameof(kelvin), kelvin, "Kelvin aralık dışında.");
        if (!Enabled) return BlockedResult;

        return Manual(blueLight.SetKelvin(kelvin).Applied);
    }

    public QuickResult Panic()
    {
        try
        {
            profiles.PanicOff();
            return new QuickResult(QuickOutcome.Done);
        }
        catch (Exception ex)
        {
            // Panik yolunda hata yutulmaz: kullanıcıya "kapanmadı" denir. ColorProfiles.PanicOff her adımı ayrı dener.
            return new QuickResult(QuickOutcome.Failed, Error: ex.Message);
        }
        finally
        {
            _active = null; // efektler (en azından kısmen) kapandı: ekranda tam bir mod yok
        }
    }

    /// <summary>Elle parlaklık ya da mavi ışık: yazıldıysa etkin mod "elle değiştirildi" olur.</summary>
    private QuickResult Manual(bool applied)
    {
        if (!applied) return new QuickResult(QuickOutcome.Failed);
        if (_active is { Adjusted: false } active) _active = active with { Adjusted = true };
        return new QuickResult(QuickOutcome.Done);
    }
}
