using System.Runtime.InteropServices;

namespace MonitorTuner.App.Views;

/// <summary>
/// Kullanıcının son klavye ya da fare girdisinden beri geçen süre (T324, mola hatırlatıcısı için). Windows <c>GetLastInputInfo</c>;
/// okunamazsa sıfır (kullanıcı etkin sayılır). Ekran ya da donanım ayarı değildir, sadece okur.
/// </summary>
internal static class UserIdle
{
    public static TimeSpan Get()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
        return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.Time));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);
}
