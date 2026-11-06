using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using MonitorTuner.App.Localization;
using MonitorTuner.Color;
using MonitorTuner.Services;

namespace MonitorTuner.App.ViewModels;

/// <summary>Ana penceredeki bir görüntü modu (T319): hazır ya da kullanıcı renk profili (T308).</summary>
/// <param name="Name">Servisin bildiği sabit ad (hazırlarda Türkçe); seçimde servise bu gider.</param>
/// <param name="Title">Arayüz dilinde ad.</param>
/// <param name="Purpose">Ne zaman kullanılır; sadece hazır modlarda (araştırma: 08-GORUNTU-YONTEMLERI.md, 00-DONANIM.md).</param>
/// <param name="Values">Ne ayarlar: arka ışık ya da ek karartma, mavi ışık, ton eğrisi, canlılık.</param>
/// <param name="Recommended">Günlük kullanım için önerilen mod (Gündüz: efektsiz, 6500K).</param>
/// <param name="Group">Grubu (T321): sinema, uzun kullanım, günlük, özel, kendi modların.</param>
/// <param name="GroupTitle">Grubun arayüz dilindeki adı.</param>
/// <param name="IsActive">Ekranda olan mod (T322, <see cref="IQuickControls.Active"/>).</param>
public sealed record ModeItem(
    string Name, string Title, string Purpose, string Values, bool Recommended, ProfileGroup Group = ProfileGroup.User,
    string GroupTitle = "", bool IsActive = false)
{
    public bool HasPurpose => Purpose.Length > 0;
}

/// <summary>Ana penceredeki bir mod grubu (T322): başlık, simge (Segoe Fluent Icons), varsa ipucu ve kartlar.</summary>
public sealed record ModeGroup(ProfileGroup Group, string Title, string Icon, string Hint, IReadOnlyList<ModeItem> Modes)
{
    public bool HasHint => Hint.Length > 0;
}

/// <summary>
/// Görüntü modları (T319). Renk profilleri eskiden sadece tepsi menüsündeydi; ana pencerede görünmediği için kullanıcı "başka mod
/// yok" sandı. Donanıma dokunmaz, sadece <see cref="IQuickControls"/> ile konuşur (sihirbaz açıkken kapalı kuralı orada). Seçim
/// tek tıklamadır ve <see cref="IWorkLane"/>'de arka planda çalışır (T316); sürerken liste kapalı görünür. Hiçbir şeyi kendiliğinden
/// uygulamaz.
/// </summary>
public sealed class ModesViewModel : INotifyPropertyChanged
{
    private const string RecommendedName = "Gündüz";
    private static readonly DimmingPlanner Planner = new();

    private readonly IQuickControls _quick;
    private readonly IWorkLane _lane;
    private string _status = "";
    private string _activeText = "";
    private ActiveMode? _shownActive;
    private int _busy;

    /// <param name="lane">Verilmezse her şey çağıranın iş parçacığında hemen çalışır (testler).</param>
    public ModesViewModel(IQuickControls quick, IWorkLane? lane = null)
    {
        _quick = quick;
        _lane = lane ?? WorkLane.Inline;
        Reload();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ModeItem> Modes { get; } = [];

    /// <summary>Kartlar gruplarıyla (T322), <see cref="ColorProfiles.GroupOrder"/> sırasında; boş grup yok.</summary>
    public ObservableCollection<ModeGroup> Groups { get; } = [];

    /// <summary>Ekranda hangi mod var (ya da yok); her <see cref="Reload"/>'da servisten okunur.</summary>
    public string ActiveText
    {
        get => _activeText;
        private set
        {
            _activeText = value;
            Raise();
        }
    }

    /// <summary>Son seçimin sonucu (uygulandı, tam uygulanamadı ve neyin uygulanmadığı, sihirbaz açık, hata).</summary>
    public string Status
    {
        get => _status;
        private set
        {
            _status = value;
            Raise();
        }
    }

    public bool IsBusy => _busy > 0;

    public bool IsReady => !IsBusy;

    /// <summary>
    /// Listeyi yeniden kurar (pencere her açıldığında; kullanıcı profilleri değişmiş olabilir), grup sırasıyla (T321; grup içinde
    /// servisin sırası). Hiçbir şey yazmaz.
    /// </summary>
    public void Reload()
    {
        var active = _quick.Active;
        _shownActive = active;
        Modes.Clear();
        foreach (var profile in _quick.Profiles.OrderBy(p => GroupIndex(p.Group)))
            Modes.Add(ToItem(profile, active is not null && string.Equals(active.Name, profile.Name, StringComparison.OrdinalIgnoreCase)));

        Groups.Clear();
        foreach (var group in Modes.GroupBy(m => m.Group))
            Groups.Add(new ModeGroup(group.Key, ProfileText.GroupTitle(group.Key), GroupIcon(group.Key), GroupHint(group.Key), [.. group]));

        ActiveText = active is null
            ? Loc.Get("Modes.Active_None")
            : Format(active.Adjusted ? "Modes.Active_Adjusted" : "Modes.Active", ProfileText.DisplayName(active.Name));
    }

    /// <summary>
    /// Pencere öne gelince (tepsiden mod seçilmiş olabilir): etkin mod değiştiyse listeyi yeniden kurar, değişmediyse hiçbir şey yapmaz
    /// (her odaklanmada kartlar yeniden kurulsaydı pencereye dönüş tıklaması kaybolurdu). Uygulama sürerken bir şey yapmaz.
    /// </summary>
    public void RefreshActive()
    {
        if (IsBusy || Equals(_quick.Active, _shownActive)) return;
        Reload();
    }

    /// <summary>Grubun simgesi (<see cref="Glyphs"/>).</summary>
    private static string GroupIcon(ProfileGroup group) => group switch
    {
        ProfileGroup.Cinema => Glyphs.Cinema,
        ProfileGroup.Comfort => Glyphs.Eye,
        ProfileGroup.Daily => Glyphs.Day,
        ProfileGroup.Special => Glyphs.Game,
        _ => Glyphs.User,
    };

    /// <summary>Sinema ve uzun kullanım gruplarının ipucu (72 Hz; oda ışığı ve 20-20-20); diğerlerinde yok.</summary>
    private static string GroupHint(ProfileGroup group) =>
        group is ProfileGroup.Cinema or ProfileGroup.Comfort ? Loc.Get("Modes.GroupHint_" + group) : "";

    private static int GroupIndex(ProfileGroup group)
    {
        var index = ColorProfiles.GroupOrder.ToList().IndexOf(group);
        return index < 0 ? int.MaxValue : index;
    }

    /// <summary>Kullanıcı moda tıkladı: profil bir kez uygulanır. Hata yutulmaz, kullanıcıya söylenir; uygulama düşmez.</summary>
    public Task Apply(ModeItem mode) => Busy(async () =>
    {
        try
        {
            var result = await _lane.RunAsync(() => _quick.SelectProfile(mode.Name));
            Status = result.Outcome switch
            {
                QuickOutcome.Done => Format("Modes.Done", mode.Title),
                QuickOutcome.Partial => Format("Modes.Partial", mode.Title, ProfileText.NotApplied(result.Profile)),
                QuickOutcome.Blocked => Loc.Get("Modes.Blocked"),
                QuickOutcome.NotFound => Loc.Get("Modes.NotFound"),
                _ => Format("Modes.Failed", mode.Title),
            };
            Reload(); // etkin mod işareti (T322)
        }
        catch (Exception ex)
        {
            Status = Format("Modes.Unexpected", ex.Message);
        }
    });

    private static ModeItem ToItem(ColorProfile profile, bool isActive)
    {
        var purpose = ProfileText.BuiltInKey(profile.Name) is { } key ? Loc.Get("Modes.Purpose_" + key) : "";
        return new ModeItem(profile.Name, ProfileText.DisplayName(profile.Name), purpose, Values(profile),
            string.Equals(profile.Name, RecommendedName, StringComparison.OrdinalIgnoreCase), profile.Group,
            ProfileText.GroupTitle(profile.Group), isActive);
    }

    /// <summary>Profilin ne ayarladığı; null ayar (profil dokunmuyor) yazılmaz. Arka ışık yüzdesi tek kaydırıcının planından (T306).</summary>
    private static string Values(ColorProfile profile)
    {
        var parts = new List<string>();
        if (profile.Brightness is { } slider)
        {
            var plan = Planner.Plan(slider);
            parts.Add(plan.GpuScale < 1.0 ? Loc.Get("Modes.Value_Dimming") : Format("Modes.Value_Backlight", plan.Backlight));
        }

        if (profile.Kelvin is { } kelvin)
            parts.Add(kelvin >= KelvinCalculator.OffKelvin ? Loc.Get("Modes.Value_BlueLightOff") : Format("Modes.Value_Kelvin", kelvin));

        // Standart ton yazılmaz (kalibrasyonun kendisi); sinema ve canlı kontrast adıyla (T320).
        if (profile.Tone != ToneStyle.Standard) parts.Add(Loc.Get("Modes.Value_Tone" + profile.Tone));

        // null canlılık: uygulamanın açtığı canlılık kapanır (ColorProfiles.Apply), yani "canlılık yok".
        parts.Add(profile.Vibrance is { } vibrance ? Format("Modes.Value_Vibrance", vibrance) : Loc.Get("Modes.Value_VibranceOff"));
        return string.Join(" · ", parts);
    }

    private async Task Busy(Func<Task> work)
    {
        _busy++;
        RaiseBusy();
        try
        {
            await work();
        }
        finally
        {
            _busy--;
            RaiseBusy();
        }
    }

    private void RaiseBusy()
    {
        Raise(nameof(IsBusy));
        Raise(nameof(IsReady));
    }

    private static string Format(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, Loc.Get(key), args);

    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
