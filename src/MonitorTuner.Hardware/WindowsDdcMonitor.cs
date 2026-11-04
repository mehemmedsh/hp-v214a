using System.Runtime.InteropServices;
using System.Text;
using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;

namespace MonitorTuner.Hardware;

/// <summary>
/// DDC/CI, Windows (dxva2, T301). Her çağrıda ekranın fiziksel monitörünü açar, işi yapar, kapatır; durum tutmaz.
/// İzin listesi ve yazma sınırı burada değil (<c>DdcControl</c>, Services). Ekranda tek fiziksel monitör yoksa
/// (çoğaltılmış ekran) hiçbir şey yapmaz: yanlış monitöre yazılmasın. Okumalar DDC'nin ara sıra cevap vermemesine
/// karşı 3 kez denenir; yazma bir kez (iki kez yazmak belleği iki kez yıpratır).
/// </summary>
public sealed class WindowsDdcMonitor : IDdcMonitor
{
    private const int ReadAttempts = 3;

    public string? ReadCapabilities(DisplayId display) => WithMonitor(display, h =>
    {
        for (var i = 0; i < ReadAttempts; i++)
        {
            if (!GetCapabilitiesStringLength(h, out var length) || length == 0) continue;
            var buffer = new byte[length];
            if (CapabilitiesRequestAndCapabilitiesReply(h, buffer, length))
                return Encoding.ASCII.GetString(buffer).TrimEnd('\0');
        }
        return null;
    });

    public DdcValue? Read(DisplayId display, byte code) => WithMonitor(display, h =>
    {
        for (var i = 0; i < ReadAttempts; i++)
            if (GetVCPFeatureAndVCPFeatureReply(h, code, out _, out var current, out var maximum))
                return new DdcValue((ushort)current, (ushort)maximum);
        return (DdcValue?)null;
    });

    public bool Write(DisplayId display, byte code, ushort value, RestorePoint restorePoint)
    {
        ArgumentNullException.ThrowIfNull(restorePoint);
        if (restorePoint.Request != new WriteRequest(WriteTarget.Ddc, display, code))
            throw new ArgumentException("Geri alma noktası bu DDC yazması için değil.", nameof(restorePoint));
        if (SafetyLimits.IsDdcWriteForbidden(code))
            throw new ArgumentException($"DDC {code:X2} koduna yazılmaz (03-GUVENLIK.md → 3).", nameof(code));
        return WithMonitor(display, h => SetVCPFeature(h, code, value));
    }

    /// <summary>Bağlı ekranların aygıt adları (<c>\\.\DISPLAY1</c> gibi); okuma, yazmaz.</summary>
    public static IReadOnlyList<DisplayId> ListDisplays()
    {
        var result = new List<DisplayId>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, _, _, _) =>
        {
            if (DeviceName(hMonitor) is { } name) result.Add(new DisplayId(name));
            return true;
        }, IntPtr.Zero);
        return result;
    }

    private static T? WithMonitor<T>(DisplayId display, Func<IntPtr, T?> action)
    {
        var hMonitor = FindMonitor(display);
        if (hMonitor == IntPtr.Zero || !GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out var count) || count != 1)
            return default;
        var monitors = new PhysicalMonitor[1];
        if (!GetPhysicalMonitorsFromHMONITOR(hMonitor, 1, monitors)) return default;
        try
        {
            return action(monitors[0].Handle);
        }
        finally
        {
            DestroyPhysicalMonitors(1, monitors);
        }
    }

    private static IntPtr FindMonitor(DisplayId display)
    {
        var found = IntPtr.Zero;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, _, _, _) =>
        {
            if (!string.Equals(DeviceName(hMonitor), display.Value, StringComparison.OrdinalIgnoreCase)) return true;
            found = hMonitor;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    private static string? DeviceName(IntPtr hMonitor)
    {
        var info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>() };
        return GetMonitorInfoW(hMonitor, ref info) ? info.DeviceName : null;
    }

    // ---- Windows API ----

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PhysicalMonitor
    {
        public IntPtr Handle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public Rect Monitor, Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MonitorInfoEx info);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint count);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint size, [Out] PhysicalMonitor[] monitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyPhysicalMonitors(uint size, PhysicalMonitor[] monitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCapabilitiesStringLength(IntPtr hMonitor, out uint length);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CapabilitiesRequestAndCapabilitiesReply(IntPtr hMonitor, [Out] byte[] buffer, uint length);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVCPFeatureAndVCPFeatureReply(
        IntPtr hMonitor, byte code, out uint type, out uint current, out uint maximum);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetVCPFeature(IntPtr hMonitor, byte code, uint value);
}
