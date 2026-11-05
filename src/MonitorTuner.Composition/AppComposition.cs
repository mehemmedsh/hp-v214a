using System.Reflection;
using MonitorTuner.Core.Profiles;
using MonitorTuner.Hardware;
using MonitorTuner.Services;

namespace MonitorTuner.Composition;

/// <summary>
/// Uygulamanın bağlama kökü (T314): gerçek Windows donanımını <see cref="AppRuntime"/>'a verir. Mantık yok; servislerin
/// kurulumu, açılış ve kapanış sırası <see cref="AppRuntime"/>'da (sahte donanımla testli). Arayüz (App) Hardware'i hiç görmez,
/// donanıma sadece buradan dolaylı ulaşır. Oluşturmak hiçbir şeye yazmaz.
/// </summary>
public static class AppComposition
{
    /// <summary>Gömülü monitör profilleri (<c>profiles/*.json</c>); derlemeye <c>profiles/&lt;ad&gt;.json</c> adıyla girer.</summary>
    internal const string ProfilePrefix = "profiles/";

    /// <summary>Ayarlar <c>%AppData%\MonitorTuner</c>'a yazılır; yedek klasörü <c>backups</c> (kurtarma dosyası da orayı okur).</summary>
    public static AppLaunch Create() => Create(JsonSettingsStore.DefaultRoot(), TimeProvider.System);

    /// <param name="settingsRoot">Ayar ve yedek klasörlerinin üstü.</param>
    public static AppLaunch Create(string settingsRoot, TimeProvider time)
    {
        var ports = new AppPorts(
            new WindowsDisplayEnumerator(), new WindowsDdcMonitor(), new WindowsGpuGammaTable(), new WindowsDisplayModes(),
            new WindowsNvidiaSettings(), new WindowsGdiIcmGammaRange(), new WindowsColorConflictProbe());
        return AppRuntime.Create(ports, LoadProfiles(), new JsonSettingsStore(settingsRoot, time), time);
    }

    /// <summary>Gömülü profilleri okur ve doğrular; geçersiz profil <see cref="InvalidProfileException"/> atar (kısmen yüklenmez).</summary>
    public static IReadOnlyList<MonitorProfile> LoadProfiles()
    {
        var assembly = typeof(AppComposition).Assembly;
        var reader = new MonitorProfileReader();
        var profiles = new List<MonitorProfile>();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ProfilePrefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var text = new StreamReader(stream, System.Text.Encoding.UTF8);
            profiles.Add(reader.Read(text.ReadToEnd()));
        }
        return profiles;
    }
}
