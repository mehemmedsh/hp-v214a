using System.Diagnostics;
using MonitorTuner.Services;

namespace MonitorTuner.Composition;

/// <summary>
/// Mavi ışık filtresiyle çakışan programlar (T307, T314). Şimdilik sadece f.lux: çalışan süreç adından anlaşılır. Windows Gece ışığı
/// sistem içi bir ayar olduğundan süreçle tespit edilemez; bu sınıf onu bildirmez. Gece ışığı GPU tablosunu değiştirirse
/// renk boru hattının dış değişiklik izlemesi kullanıcının tablosunu geri yükler ve uyarı verir (<c>AppRuntime.Tick</c>).
/// Sadece okur.
/// </summary>
public sealed class WindowsColorConflictProbe : IColorConflictProbe
{
    public IReadOnlyList<ColorConflict> Detect()
    {
        try
        {
            return Process.GetProcessesByName("flux").Length > 0 ? [ColorConflict.Flux] : [];
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or PlatformNotSupportedException)
        {
            return [];
        }
    }
}
