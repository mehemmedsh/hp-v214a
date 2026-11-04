using System.Runtime.InteropServices;
using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;

namespace MonitorTuner.Hardware;

/// <summary>
/// GPU renk tablosu, Windows gamma tablosu (gdi32, T302). Her çağrıda ekranın aygıt bağlamını açıp kapatır; durum tutmaz.
/// Katman birleştirme ve dış değişiklik izleme burada değil (<c>ColorPipeline</c>, Services). Windows derin karartma
/// ve çok sıcak tabloları <c>GdiIcmGammaRange</c> 256 değilse reddeder: o zaman <see cref="Write"/> false döner.
/// </summary>
public sealed class WindowsGpuGammaTable : IGpuGammaTable
{
    public GammaRamp? Read(DisplayId display) => WithDc(display, hdc =>
    {
        var ramp = new ushort[3 * GammaRamp.Size];
        if (!GetDeviceGammaRamp(hdc, ramp)) return null;
        return new GammaRamp(ramp[..GammaRamp.Size], ramp[GammaRamp.Size..(2 * GammaRamp.Size)], ramp[(2 * GammaRamp.Size)..]);
    });

    [OnlyColorPipelineMayCall]
    public bool Write(DisplayId display, GammaRamp table, RestorePoint restorePoint)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(restorePoint);
        if (restorePoint.Request != new WriteRequest(WriteTarget.GpuGammaTable, display))
            throw new ArgumentException("Geri alma noktası bu GPU tablosu yazması için değil.", nameof(restorePoint));
        ushort[] ramp = [.. table.Red, .. table.Green, .. table.Blue];
        return WithDc(display, hdc => SetDeviceGammaRamp(hdc, ramp));
    }

    private static T? WithDc<T>(DisplayId display, Func<IntPtr, T?> action)
    {
        var hdc = CreateDCW(null, display.Value, null, IntPtr.Zero);
        if (hdc == IntPtr.Zero) return default; // ekran yok ya da bağlı değil
        try
        {
            return action(hdc);
        }
        finally
        {
            DeleteDC(hdc);
        }
    }

    // ---- Windows API ----

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateDCW(string? driver, string device, string? output, IntPtr devMode);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDeviceGammaRamp(IntPtr hdc, [Out] ushort[] ramp);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDeviceGammaRamp(IntPtr hdc, ushort[] ramp);
}
