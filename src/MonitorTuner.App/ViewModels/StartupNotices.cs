using System.Globalization;
using MonitorTuner.App.Localization;
using MonitorTuner.Core;
using MonitorTuner.Services;

namespace MonitorTuner.App.ViewModels;

/// <summary>
/// Açılış ve zamanlayıcı iletileri (T314). Mantık yok: servislerin raporunu kullanıcıya söylenecek metne çevirir. Başarısız her adım
/// söylenir (hata yutulmaz); başarılı açılış sessizdir.
/// </summary>
public static class StartupNotices
{
    public static IReadOnlyList<string> From(StartupReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var notices = new List<string>();
        if (!report.FullGammaRange) notices.Add(Loc.Get("Startup.NoFullGammaRange"));
        if (!report.TableApplied) notices.Add(Loc.Get("Startup.TableRejected"));
        return notices;
    }

    /// <summary>Başka program GPU renk tablosunu değiştirdi, kullanıcının tablosu geri yüklendi.</summary>
    public static string ExternalTableRestored() => Loc.Get("Startup.ExternalTable");

    /// <summary>Hiçbir ekran bir profille eşleşmedi: görülenler listelenir, hiçbir şeye yazılmadığı söylenir.</summary>
    public static string NoSupportedMonitor(IReadOnlyList<MonitorInfo> seen)
    {
        ArgumentNullException.ThrowIfNull(seen);
        var list = seen.Count == 0
            ? Loc.Get("Startup.NoDisplaysSeen")
            : string.Join(", ", seen.Select(m => m.Model.Length > 0
                ? Format("Startup.SeenDisplay", m.Model, m.Display.Value)
                : Format("Startup.UnreadableDisplay", m.Display.Value)));
        return Format("Startup.NoSupportedMonitor", list);
    }

    public static string ProfileError(string reason) => Format("Startup.ProfileError", reason);

    public static string StartFailed(string reason) => Format("Startup.StartFailed", reason);

    private static string Format(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, Loc.Get(key), args);
}
