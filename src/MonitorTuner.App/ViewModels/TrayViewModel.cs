using System.Globalization;
using MonitorTuner.App.Localization;
using MonitorTuner.Color;
using MonitorTuner.Services;

namespace MonitorTuner.App.ViewModels;

/// <summary>Tepsi menüsünde bir öge. <see cref="Children"/> doluysa alt menü; <see cref="IsSeparator"/> ayraç.</summary>
public sealed record TrayItem(string Text, bool Enabled = true, Action? Click = null, IReadOnlyList<TrayItem>? Children = null, bool IsSeparator = false)
{
    public static TrayItem Separator { get; } = new("", IsSeparator: true);
}

/// <summary>
/// Sistem tepsisi menüsünün mantığı (T310). Donanıma dokunmaz, sadece <see cref="IQuickControls"/> ile konuşur; hiçbir ayarı
/// kendiliğinden değiştirmez, her yazma bir tıklama ya da panik kısayoludur. Ayar sihirbazı açıkken profil, parlaklık ve mavi
/// ışık ögeleri kapalıdır (menü her açılışta <see cref="BuildMenu"/> ile yeniden kurulur); panik hep açıktır.
/// </summary>
public sealed class TrayViewModel(IQuickControls quick, string hotKeyText)
{
    // Kaydırıcı değerleri (tek kaydırıcı): 0,2 üstü sadece arka ışık, altı ek karartma (arka ışık 0'da kalır).
    private static readonly double[] BrightnessLevels = [1.0, 0.8, 0.6, 0.4, 0.2];
    private static readonly (string Key, double Slider)[] DimmingLevels =
        [("Light", 0.15), ("Medium", 0.10), ("Strong", 0.05), ("Darkest", 0.0)];
    private static readonly int[] BlueLightKelvins = [5000, 3400, 2700];

    /// <summary>Kullanıcıya gösterilecek kısa ileti (tepsi balonu). Arayüz iş parçacığında çağrılır.</summary>
    public event Action<string>? Notified;

    public Action? OpenMainWindow { get; set; }
    public Action? Exit { get; set; }

    public IReadOnlyList<TrayItem> BuildMenu()
    {
        var enabled = quick.Enabled;
        var items = new List<TrayItem>();
        if (!enabled) items.Add(new TrayItem(Loc.Get("Tray.WizardActive"), Enabled: false));

        items.Add(new TrayItem(Loc.Get("Tray.Profiles"), Children:
            [.. quick.Profiles.Select(p => new TrayItem(p.Name, enabled, () => SelectProfile(p.Name)))]));

        items.Add(new TrayItem(Loc.Get("Tray.Brightness"), Children:
            [.. BrightnessLevels.Select(level =>
            {
                var percent = ((int)Math.Round(level * 100)).ToString(CultureInfo.CurrentCulture);
                return new TrayItem(Format("Tray.BrightnessLevel", percent), enabled,
                    () => SetBrightness(level, Format("Tray.Done_Brightness", percent)));
            })]));

        items.Add(new TrayItem(Loc.Get("Tray.Dimming"), Children:
            [.. DimmingLevels.Select(level =>
            {
                var name = Loc.Get("Tray.Dimming_" + level.Key);
                return new TrayItem(name, enabled, () => SetBrightness(level.Slider, Format("Tray.Done_Dimming", name)));
            })]));

        items.Add(new TrayItem(Loc.Get("Tray.BlueLight"), Children:
        [
            new TrayItem(Loc.Get("Tray.BlueLightOff"), enabled, () => SetBlueLight(KelvinCalculator.OffKelvin)),
            .. BlueLightKelvins.Select(k => new TrayItem(Format("Tray.BlueLightKelvin", k), enabled, () => SetBlueLight(k))),
        ]));

        items.Add(TrayItem.Separator);
        items.Add(new TrayItem(Format("Tray.Panic", hotKeyText), Click: Panic));
        items.Add(TrayItem.Separator);
        items.Add(new TrayItem(Loc.Get("Tray.Open"), Click: () => OpenMainWindow?.Invoke()));
        items.Add(new TrayItem(Loc.Get("Tray.Exit"), Click: () => Exit?.Invoke()));
        return items;
    }

    public void SelectProfile(string name) => Guard(() =>
    {
        var result = quick.SelectProfile(name);
        Notify(result.Outcome switch
        {
            QuickOutcome.Done => Format("Tray.Done_Profile", name),
            QuickOutcome.Partial => Format("Tray.Partial_Profile", name, NotApplied(result.Profile)),
            _ => Generic(result),
        });
    });

    public void SetBlueLight(int kelvin) => Guard(() =>
    {
        var result = quick.SetBlueLight(kelvin);
        Notify(result.Outcome == QuickOutcome.Done
            ? kelvin == KelvinCalculator.OffKelvin ? Loc.Get("Tray.Done_BlueLightOff") : Format("Tray.Done_BlueLight", kelvin)
            : Generic(result));
    });

    /// <summary>Panik kısayolu ve menü ögesi. Sihirbaz açıkken de çalışır.</summary>
    public void Panic() => Guard(() =>
    {
        var result = quick.Panic();
        Notify(result.Outcome == QuickOutcome.Done ? Loc.Get("Tray.PanicDone") : Format("Tray.PanicFailed", result.Error ?? "?"));
    });

    private void SetBrightness(double slider, string doneText) => Guard(() =>
    {
        var result = quick.SetBrightness(slider);
        Notify(result.Outcome == QuickOutcome.Done ? doneText : Generic(result));
    });

    private static string Generic(QuickResult result) => result.Outcome switch
    {
        QuickOutcome.Blocked => Loc.Get("Tray.Blocked"),
        QuickOutcome.NotFound => Loc.Get("Tray.NotFound"),
        _ => Loc.Get("Tray.Failed"),
    };

    /// <summary>Uygulanamayan ya da ertelenen (ekranda henüz olmayan) ayarların adları.</summary>
    private static string NotApplied(ProfileApplyResult? profile)
    {
        if (profile is null) return "";
        var names = new List<string>();
        if (profile.Brightness is ProfileStep.Failed or ProfileStep.Deferred) names.Add(Loc.Get("Tray.Step_Brightness"));
        if (profile.BlueLight is ProfileStep.Failed or ProfileStep.Deferred) names.Add(Loc.Get("Tray.Step_BlueLight"));
        if (profile.Vibrance is ProfileStep.Failed or ProfileStep.Deferred) names.Add(Loc.Get("Tray.Step_Vibrance"));
        return string.Join(", ", names);
    }

    // Tepsi tıklaması işlenmeyen istisnayla uygulamayı düşürmesin; hata kullanıcıya söylenir.
    private void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Notify(Format("Tray.Unexpected", ex.Message));
        }
    }

    private void Notify(string text) => Notified?.Invoke(text);

    private static string Format(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, Loc.Get(key), args);
}
