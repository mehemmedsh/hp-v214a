using System.Runtime.InteropServices;
using MonitorTuner.Core;

namespace MonitorTuner.Hardware;

/// <summary>
/// Masaüstüne bağlı ekranlar (user32 <c>EnumDisplayDevices</c>, T104). Kurtarma dosyası hepsini sıfırlar: kablo başka
/// çıkışa takılınca monitörün ekran kimliği değişir. Yansıtma sürücüleri (uzak masaüstü vb.) sayılmaz. Yazmaz.
/// </summary>
public static class WindowsDisplayList
{
    private const int AttachedToDesktop = 0x1, MirroringDriver = 0x8;

    public static IReadOnlyList<DisplayId> ListActive()
    {
        var found = new List<DisplayId>();
        for (uint i = 0; ; i++)
        {
            var device = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
            if (!EnumDisplayDevices(null, i, ref device, 0)) return found;
            if ((device.StateFlags & AttachedToDesktop) != 0 && (device.StateFlags & MirroringDriver) == 0)
                found.Add(new DisplayId(device.DeviceName));
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplayDevicesW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevices(string? device, uint index, ref DisplayDevice info, uint flags);
}
