using System.Runtime.InteropServices;
using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;

namespace MonitorTuner.Hardware;

/// <summary>
/// Windows ekran modları (user32, T209). Sadece Windows'un listesindeki modlar arasında geçer; mod eklemez, silmez.
/// <see cref="SwitchMode"/> anlık geçiştir (kayıt defterine yazmaz), <see cref="PersistMode"/> şu anki modu kaydeder.
/// Durum tutmaz; her çağrı Windows'tan yeniden okur.
/// </summary>
public sealed class WindowsDisplayModes : IDisplayModes
{
    public IReadOnlyList<DisplayMode> ListModes(DisplayId display)
    {
        var current = Current(display);
        var result = new List<DisplayMode>();
        for (var i = 0; EnumMode(display, i) is { } dm; i++)
        {
            // Sadece masaüstünün renk derinliği ve yönü, taramalı (interlaced) modlar hariç.
            if ((dm.dmDisplayFlags & DmInterlaced) != 0) continue;
            if (current is { } c && (dm.dmBitsPerPel != c.dmBitsPerPel || dm.dmDisplayOrientation != c.dmDisplayOrientation)) continue;
            var mode = ToMode(dm);
            if (!result.Contains(mode)) result.Add(mode);
        }
        return result;
    }

    public DisplayMode? GetCurrentMode(DisplayId display) => Current(display) is { } dm ? ToMode(dm) : null;

    /// <summary>Windows'a kayıtlı mod (yeniden başlatmada gelecek olan). 🔧 testi bununla kontrol eder.</summary>
    public static DisplayMode? ReadSavedMode(DisplayId display) => EnumMode(display, EnumRegistrySettings) is { } dm ? ToMode(dm) : null;

    public bool SwitchMode(DisplayId display, DisplayMode mode, RestorePoint restorePoint)
    {
        Check(display, restorePoint);
        if (!ListModes(display).Contains(mode) || Current(display) is not { } dm) return false;
        dm.dmPelsWidth = mode.Width;
        dm.dmPelsHeight = mode.Height;
        dm.dmDisplayFrequency = mode.RefreshHz;
        dm.dmFields = DmPelsWidth | DmPelsHeight | DmDisplayFrequency;
        // Önce Windows'a sor (CDS_TEST), sonra anlık geç (0: kayıt defterine yazılmaz).
        return Change(display, ref dm, CdsTest) && Change(display, ref dm, 0);
    }

    public bool PersistMode(DisplayId display, DisplayMode mode, RestorePoint restorePoint)
    {
        Check(display, restorePoint);
        if (Current(display) is not { } dm || ToMode(dm) != mode) return false;
        dm.dmFields = DmPelsWidth | DmPelsHeight | DmDisplayFrequency;
        // Mod zaten ekranda: CDS_UPDATEREGISTRY yeni geçiş yapmadan kaydeder. Kaydın tuttuğu ayrıca okunarak doğrulanır.
        return Change(display, ref dm, CdsUpdateRegistry) && ReadSavedMode(display) == mode;
    }

    private static void Check(DisplayId display, RestorePoint restorePoint)
    {
        ArgumentNullException.ThrowIfNull(restorePoint);
        if (restorePoint.Request != new WriteRequest(WriteTarget.DisplayMode, display))
            throw new ArgumentException("Geri alma noktası bu ekran modu yazması için değil.", nameof(restorePoint));
    }

    private static DisplayMode ToMode(DevMode dm) => new(dm.dmPelsWidth, dm.dmPelsHeight, dm.dmDisplayFrequency);

    private static DevMode? Current(DisplayId display) => EnumMode(display, EnumCurrentSettings);

    private static DevMode? EnumMode(DisplayId display, int index)
    {
        var dm = new DevMode { dmSize = (short)Marshal.SizeOf<DevMode>() };
        return EnumDisplaySettingsExW(display.Value, index, ref dm, 0) ? dm : null;
    }

    private static bool Change(DisplayId display, ref DevMode dm, uint flags) =>
        ChangeDisplaySettingsExW(display.Value, ref dm, IntPtr.Zero, flags, IntPtr.Zero) == DispChangeSuccessful;

    // ---- Windows API ----

    private const int EnumCurrentSettings = -1, EnumRegistrySettings = -2;
    private const int DmPelsWidth = 0x80000, DmPelsHeight = 0x100000, DmDisplayFrequency = 0x400000, DmInterlaced = 0x2;
    private const uint CdsUpdateRegistry = 0x1, CdsTest = 0x2;
    private const int DispChangeSuccessful = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields;
        public int dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettingsExW(string deviceName, int modeNum, ref DevMode devMode, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettingsExW(string deviceName, ref DevMode devMode, IntPtr hwnd, uint flags, IntPtr param);
}
