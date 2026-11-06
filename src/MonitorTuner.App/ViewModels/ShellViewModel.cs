using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using MonitorTuner.App.Localization;
using MonitorTuner.Core;
using MonitorTuner.Services;

namespace MonitorTuner.App.ViewModels;

/// <summary>
/// Ana pencerenin kenar çubuğundaki bilgi (T322): monitör, bağlantı, şu anki Hz ve sürüm. Hiçbir şey yazmaz; Hz Windows'tan okunur
/// (servis çağrısı olduğu için <see cref="IWorkLane"/>'de, T316). Okunamazsa Hz gösterilmez, hata pencereyi düşürmez.
/// </summary>
public sealed class ShellViewModel : INotifyPropertyChanged
{
    private readonly string _model;
    private readonly string _connection;
    private readonly Func<int?> _readRefreshHz;
    private readonly IWorkLane _lane;
    private string _monitorLine;

    /// <param name="readRefreshHz">Windows'un şu anki Hz'i (<c>IRefreshRatePanel.CurrentRefreshHz</c>); null: okunamadı.</param>
    /// <param name="lane">Verilmezse hemen, çağıranın iş parçacığında (testler).</param>
    public ShellViewModel(string model, ConnectionType connection, Func<int?> readRefreshHz, string version, IWorkLane? lane = null)
    {
        _model = model;
        _connection = ConnectionText(connection);
        _readRefreshHz = readRefreshHz;
        _lane = lane ?? WorkLane.Inline;
        _monitorLine = Format("Shell.MonitorLine_NoHz", _model, _connection);
        Version = Format("Shell.Version", version);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>"HP P21b G4 · HDMI · 76 Hz"; Hz okunamadıysa Hz'siz.</summary>
    public string MonitorLine
    {
        get => _monitorLine;
        private set
        {
            if (_monitorLine == value) return;
            _monitorLine = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MonitorLine)));
        }
    }

    public string Version { get; }

    /// <summary>Pencere açılınca ve öne gelince: Hz yeniden okunur (kullanıcı Windows'tan değiştirmiş olabilir).</summary>
    public async Task Refresh()
    {
        int? hz;
        try
        {
            hz = await _lane.RunAsync(_readRefreshHz);
        }
        catch (Exception)
        {
            hz = null; // yalnız gösterim: okunamayan Hz yazılmaz
        }

        MonitorLine = hz is { } value
            ? Format("Shell.MonitorLine", _model, _connection, value)
            : Format("Shell.MonitorLine_NoHz", _model, _connection);
    }

    /// <summary>Bağlantı adları her dilde aynı (HDMI, DisplayPort); yalnız bilinmeyen çevrilir.</summary>
    private static string ConnectionText(ConnectionType connection) => connection switch
    {
        ConnectionType.Hdmi => "HDMI",
        ConnectionType.DisplayPort => "DisplayPort",
        ConnectionType.Dvi => "DVI",
        ConnectionType.Vga => "VGA",
        _ => Loc.Get("Shell.Connection_Unknown"),
    };

    private static string Format(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, Loc.Get(key), args);
}
