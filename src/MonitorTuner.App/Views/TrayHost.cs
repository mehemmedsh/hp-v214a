using System.Runtime.InteropServices;
using System.Windows.Interop;
using MonitorTuner.App.Localization;
using MonitorTuner.App.ViewModels;
using WinForms = System.Windows.Forms;

namespace MonitorTuner.App.Views;

/// <summary>
/// Sistem tepsisi simgesi ve panik kısayolu (T310). Mantık yok: menüyü <see cref="TrayViewModel"/> kurar, simge ve kısayol
/// sadece onu çağırır. Menü her açılışta yeniden kurulur (sihirbaz durumu güncel olsun). Panik kısayolu
/// Ctrl+Alt+Shift+P (<see cref="HotKeyText"/>); başka program tutuyorsa kayıt başarısız olur, kullanıcıya söylenir ve panik
/// tepsi menüsünde kalır. Arayüz iş parçacığında oluşturulur ve kullanılır; <see cref="Dispose"/> simgeyi ve kısayolu bırakır.
/// </summary>
public sealed class TrayHost : IDisposable
{
    public const string HotKeyText = "Ctrl+Alt+Shift+P";

    private const int WmHotKey = 0x0312;
    private const int HotKeyId = 0x7310;
    private const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4, ModNoRepeat = 0x4000;
    private const uint VkP = 0x50;
    private static readonly nint HwndMessage = -3; // yalnız mesaj alan pencere

    private readonly TrayViewModel _viewModel;
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ContextMenuStrip _menu = new();
    private readonly HwndSource _messageWindow;

    public TrayHost(TrayViewModel viewModel)
    {
        _viewModel = viewModel;
        _icon = new WinForms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = Loc.Get("Tray.Tooltip"),
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _menu.Opening += (_, _) => Rebuild();
        _icon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left) _viewModel.OpenMainWindow?.Invoke();
        };
        _viewModel.Notified += Balloon;

        _messageWindow = new HwndSource(new HwndSourceParameters("MonitorTunerTray") { ParentWindow = HwndMessage });
        _messageWindow.AddHook(OnMessage);
        if (!RegisterHotKey(_messageWindow.Handle, HotKeyId, ModControl | ModAlt | ModShift | ModNoRepeat, VkP))
            Balloon(string.Format(System.Globalization.CultureInfo.CurrentCulture, Loc.Get("Tray.HotKeyFailed"), HotKeyText));
    }

    public void Dispose()
    {
        _viewModel.Notified -= Balloon;
        UnregisterHotKey(_messageWindow.Handle, HotKeyId);
        _messageWindow.Dispose();
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }

    private nint OnMessage(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmHotKey && wParam == HotKeyId)
        {
            handled = true;
            _viewModel.Panic();
        }

        return 0;
    }

    private void Rebuild()
    {
        _menu.Items.Clear();
        foreach (var item in _viewModel.BuildMenu()) _menu.Items.Add(ToStripItem(item));
    }

    private static WinForms.ToolStripItem ToStripItem(TrayItem item)
    {
        if (item.IsSeparator) return new WinForms.ToolStripSeparator();

        var strip = new WinForms.ToolStripMenuItem(item.Text) { Enabled = item.Enabled };
        if (item.Children is { } children)
            foreach (var child in children) strip.DropDownItems.Add(ToStripItem(child));
        else if (item.Click is { } click)
            strip.Click += (_, _) => click();
        return strip;
    }

    private void Balloon(string text)
    {
        _icon.BalloonTipTitle = Loc.Get("Common.AppTitle");
        _icon.BalloonTipText = text;
        _icon.ShowBalloonTip(5000);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);
}
