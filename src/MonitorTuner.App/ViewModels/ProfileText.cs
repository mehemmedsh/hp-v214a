using MonitorTuner.App.Localization;
using MonitorTuner.Services;

namespace MonitorTuner.App.ViewModels;

/// <summary>
/// Renk profillerinin ortak metinleri: tepsi (T310) ve ana penceredeki görüntü modları (T319) aynı adı ve aynı "uygulanmayan"
/// listesini gösterir. Hazır profillerin adı Türkçe sabittir (servis bu adla bulur); arayüzde çevirisi görünür (T502). Kullanıcı
/// profili kullanıcının verdiği adla kalır.
/// </summary>
public static class ProfileText
{
    private static readonly IReadOnlyDictionary<string, string> BuiltInKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Film"] = "Film", ["Sinema"] = "Cinema", ["Sinema · Karanlık oda"] = "CinemaDark", ["Sinema · Canlı"] = "CinemaVivid",
        ["Sinema · Gece"] = "CinemaNight",
        ["Konfor · Gündüz"] = "ComfortDay", ["Konfor · Akşam"] = "ComfortEvening", ["Konfor · Gece"] = "ComfortNight",
        ["Gündüz"] = "Day", ["Akşam"] = "Evening", ["Gece"] = "Night", ["Kod"] = "Code",
        ["Oyun"] = "Game", ["DaVinci zaman çizelgesi"] = "DaVinci",
    };

    /// <summary>Hazır profilin çeviri anahtarı ("Day"); kullanıcı profilinde null.</summary>
    public static string? BuiltInKey(string name) => BuiltInKeys.TryGetValue(name, out var key) ? key : null;

    public static string DisplayName(string name) => BuiltInKey(name) is { } key ? Loc.Get("Tray.Profile_" + key) : name;

    /// <summary>Grubun arayüz dilindeki adı (T321): tepsinin alt menüleri ve ana penceredeki bölümler.</summary>
    public static string GroupTitle(ProfileGroup group) => Loc.Get("Modes.Group_" + group);

    /// <summary>Uygulanamayan ya da ertelenen (ekranda henüz olmayan) ayarların adları, virgülle.</summary>
    public static string NotApplied(ProfileApplyResult? profile)
    {
        if (profile is null) return "";
        var names = new List<string>();
        if (profile.Brightness is ProfileStep.Failed or ProfileStep.Deferred) names.Add(Loc.Get("Tray.Step_Brightness"));
        if (profile.BlueLight is ProfileStep.Failed or ProfileStep.Deferred) names.Add(Loc.Get("Tray.Step_BlueLight"));
        if (profile.Tone is ProfileStep.Failed or ProfileStep.Deferred) names.Add(Loc.Get("Tray.Step_Tone"));
        if (profile.Vibrance is ProfileStep.Failed or ProfileStep.Deferred) names.Add(Loc.Get("Tray.Step_Vibrance"));
        return string.Join(", ", names);
    }
}
