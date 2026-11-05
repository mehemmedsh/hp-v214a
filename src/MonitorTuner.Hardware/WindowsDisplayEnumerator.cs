using System.Runtime.InteropServices;
using Microsoft.Win32;
using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;

namespace MonitorTuner.Hardware;

/// <summary>
/// Monitör tanımanın Windows yarısı (T314): masaüstüne bağlı her ekran için ham EDID (kayıt defterinden okunur), bağlantı tipi
/// (<c>QueryDisplayConfig</c>) ve ekran kartının adı (<c>EnumDisplayDevices</c>). Sadece okur; hiçbir şeye yazmaz. EDID okunamazsa
/// ekran listede kalır, EDID'i boş olur ve hiçbir profille eşleşmez (ona yazılmaz). Bağlantı tipi okunamazsa
/// <see cref="ConnectionType.Unknown"/>; ekran kimliği gibi port adı da kararlı kalsın diye "Unknown" yazılır.
/// </summary>
public sealed class WindowsDisplayEnumerator : IDisplayEnumerator
{
    private const int Active = 0x1, AttachedToDesktop = 0x1, MirroringDriver = 0x8;
    private const uint GetDeviceInterfaceName = 0x1;
    private const uint QdcOnlyActivePaths = 0x2;
    private const uint GetSourceName = 1;

    public IReadOnlyList<ConnectedDisplay> EnumerateDisplays()
    {
        var connections = ReadConnections();
        var found = new List<ConnectedDisplay>();
        for (uint i = 0; ; i++)
        {
            var adapter = NewDevice();
            if (!EnumDisplayDevices(null, i, ref adapter, 0)) return found;
            if ((adapter.StateFlags & AttachedToDesktop) == 0 || (adapter.StateFlags & MirroringDriver) != 0) continue;

            var monitor = NewDevice();
            if (!EnumDisplayDevices(adapter.DeviceName, 0, ref monitor, GetDeviceInterfaceName) || (monitor.StateFlags & Active) == 0) continue;

            var connection = connections.GetValueOrDefault(adapter.DeviceName, ConnectionType.Unknown);
            found.Add(new ConnectedDisplay(
                new DisplayId(adapter.DeviceName), ReadEdid(monitor.DeviceID) ?? [], connection, PortName(connection), adapter.DeviceString));
        }
    }

    internal static string PortName(ConnectionType connection) => connection switch
    {
        ConnectionType.Hdmi => "HDMI",
        ConnectionType.DisplayPort => "DP",
        ConnectionType.Dvi => "DVI",
        ConnectionType.Vga => "VGA",
        _ => "Unknown",
    };

    internal static ConnectionType ConnectionOf(uint outputTechnology) => outputTechnology switch
    {
        0 => ConnectionType.Vga,                // HD15
        4 => ConnectionType.Dvi,
        5 => ConnectionType.Hdmi,
        10 or 11 => ConnectionType.DisplayPort, // harici ve gömülü
        _ => ConnectionType.Unknown,
    };

    /// <summary>
    /// Arayüz yolundan (<c>\\?\DISPLAY#HPN3430#5&amp;2a3b4c&amp;0&amp;UID4352#{guid}</c>) kayıt defteri anahtarının
    /// <c>Enum\DISPLAY\HPN3430\5&amp;2a3b4c&amp;0&amp;UID4352</c> bölümünü çıkarır. Biçim tanınmazsa null.
    /// </summary>
    internal static string? EnumKeyOf(string interfacePath)
    {
        var parts = interfacePath.TrimStart('\\', '?').Split('#');
        if (parts.Length < 3 || !parts[0].Equals("DISPLAY", StringComparison.OrdinalIgnoreCase) || parts[1].Length == 0 || parts[2].Length == 0)
            return null;
        return $@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{parts[1]}\{parts[2]}\Device Parameters";
    }

    private static byte[]? ReadEdid(string interfacePath)
    {
        try
        {
            if (EnumKeyOf(interfacePath) is not { } key) return null;
            using var parameters = Registry.LocalMachine.OpenSubKey(key, writable: false);
            return parameters?.GetValue("EDID") as byte[];
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    /// <summary>Etkin yollarda her kaynağın (\\.\DISPLAYn) çıkış tekniği. Okunamazsa boş: bağlantı tipi bilinmiyor sayılır.</summary>
    private static Dictionary<string, ConnectionType> ReadConnections()
    {
        var result = new Dictionary<string, ConnectionType>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out var pathCount, out var modeCount) != 0) return result;
            var paths = new PathInfo[pathCount];
            var modes = new ModeInfo[modeCount];
            if (QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0) return result;

            for (var i = 0; i < pathCount; i++)
            {
                var name = new SourceDeviceName
                {
                    Header = new DeviceInfoHeader
                    {
                        Type = GetSourceName,
                        Size = (uint)Marshal.SizeOf<SourceDeviceName>(),
                        AdapterId = paths[i].Source.AdapterId,
                        Id = paths[i].Source.Id,
                    },
                };
                if (DisplayConfigGetDeviceInfo(ref name) != 0) continue;
                result.TryAdd(name.ViewGdiDeviceName, ConnectionOf(paths[i].Target.OutputTechnology));
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or MarshalDirectiveException)
        {
            result.Clear();
        }
        return result;
    }

    private static DisplayDevice NewDevice() => new() { Size = Marshal.SizeOf<DisplayDevice>() };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PathSourceInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PathTargetInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint OutputTechnology;
        public uint Rotation;
        public uint Scaling;
        public uint RefreshNumerator;
        public uint RefreshDenominator;
        public uint ScanLineOrdering;
        public int TargetAvailable;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PathInfo
    {
        public PathSourceInfo Source;
        public PathTargetInfo Target;
        public uint Flags;
    }

    // DISPLAYCONFIG_MODE_INFO: içeriğine bakılmaz, sadece boyutu (64 bayt) doğru olmalı.
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    internal struct ModeInfo
    {
        public uint InfoType;
        public uint Id;
        public Luid AdapterId;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DeviceInfoHeader
    {
        public uint Type;
        public uint Size;
        public Luid AdapterId;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct SourceDeviceName
    {
        public DeviceInfoHeader Header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string ViewGdiDeviceName;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplayDevicesW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevices(string? device, uint index, ref DisplayDevice info, uint flags);

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags, ref uint pathCount, [In, Out] PathInfo[] paths, ref uint modeCount, [In, Out] ModeInfo[] modes, IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref SourceDeviceName request);
}
