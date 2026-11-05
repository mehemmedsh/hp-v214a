using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using MonitorTuner.App.Localization;
using MonitorTuner.Services;

namespace MonitorTuner.App.ViewModels;

/// <param name="Key">Sabit ad (<see cref="PanelChoice.Name"/>): açıklama metninin anahtarı.</param>
/// <param name="Verified">false: ön ayarın adı tahmin (T318); başlık ve açıklama bunu söyler.</param>
public sealed record ChoiceItem(int Value, string Title, bool Selectable, string Key = "", bool Verified = true)
{
    public override string ToString() => Title;
}

/// <summary>Panelde bir satır: kaydırıcı ya da seçim listesi. Kilitliyse nedeni <see cref="LockText"/>'te.</summary>
public sealed class PanelItemViewModel(PanelSettingState state, Action<PanelItemViewModel> commit) : INotifyPropertyChanged
{
    private double _value = state.Current;
    private ChoiceItem? _selected = Choices(state).FirstOrDefault(c => c.Value == state.Current);

    public event PropertyChangedEventHandler? PropertyChanged;

    public PanelSetting Setting { get; } = state.Setting;
    public string Title { get; } = Loc.Get("DdcPanel.Setting_" + state.Setting);
    public double Minimum { get; } = state.Minimum;
    public double Maximum { get; } = state.Maximum;
    public bool IsChoice { get; } = state.Choices.Count > 0;
    public bool IsSlider => !IsChoice;
    public bool IsEditable { get; } = state.Editable;
    public IReadOnlyList<ChoiceItem> ChoiceList { get; } = Choices(state);

    public string LockText { get; } = state.Lock == SettingLock.None ? "" : Loc.Get("DdcPanel.Lock_" + state.Lock);
    public bool HasLock => LockText.Length > 0;

    /// <summary>
    /// Satırın altındaki kısa açıklama (T318): renk ön ayarında seçili ön ayarın ne yaptığı (araştırma, 00-DONANIM.md → DDC/CI),
    /// adı tahminse nasıl doğrulanacağı; keskinlikte nötr değer ipucu. Diğer ayarlarda boş.
    /// </summary>
    public string Description => Setting switch
    {
        PanelSetting.ColorPreset when _selected is { } c => DescribePreset(c),
        PanelSetting.Sharpness => Loc.Get("DdcPanel.Hint_Sharpness"),
        _ => "",
    };

    public bool HasDescription => Description.Length > 0;

    /// <summary>Kaydırıcı değeri. Sürüklerken sadece ekranda değişir; yazma <see cref="PanelItemViewModel"/> bırakılınca (Commit) olur.</summary>
    public double Value
    {
        get => _value;
        set
        {
            _value = Math.Round(value);
            Raise();
            Raise(nameof(ValueText));
        }
    }

    public string ValueText => ((int)_value).ToString(CultureInfo.CurrentCulture);

    public ChoiceItem? SelectedChoice
    {
        get => _selected;
        set
        {
            if (value is null || value == _selected) return;
            _selected = value;
            Raise();
            Raise(nameof(Description));
            Raise(nameof(HasDescription));
            commit(this); // seçim listesinde seçmek "bırakmak" demektir: tek yazma
        }
    }

    internal int ValueToWrite => IsChoice ? _selected?.Value ?? (int)_value : (int)_value;

    private static IReadOnlyList<ChoiceItem> Choices(PanelSettingState s) => [.. s.Choices.Select(c =>
    {
        var name = Loc.Get("DdcPanel.Choice_" + c.Name);
        if (IsMissing(name)) name = c.Name; // bilinmeyen ön ayar değeri: sayı
        var title = c.Verified ? name : string.Format(CultureInfo.CurrentCulture, Loc.Get("DdcPanel.Choice_Unverified"), name);
        return new ChoiceItem(c.Value, title, c.Selectable, c.Name, c.Verified);
    })];

    private static string DescribePreset(ChoiceItem choice)
    {
        var about = Loc.Get("DdcPanel.About_" + choice.Key);
        if (IsMissing(about)) about = "";
        return choice.Verified ? about : (about + " " + Loc.Get("DdcPanel.About_Unverified")).Trim();
    }

    private static bool IsMissing(string text) => text.StartsWith("[[", StringComparison.Ordinal);

    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// DDC paneli ekranının mantığı (T305). Donanıma dokunmaz, sadece <see cref="IDdcPanel"/> ile konuşur. DDC yoksa
/// OSD talimatını gösterir; OSD'den değişen değerleri bildirir, ezmez. Okuma ve yazma <see cref="IWorkLane"/>'de arka planda
/// sırayla yapılır (T316): pencere DDC yanıtını beklerken donmaz, <see cref="IsBusy"/> süresince panel kapalı görünür.
/// </summary>
public sealed class DdcPanelViewModel : INotifyPropertyChanged
{
    private readonly IDdcPanel _panel;
    private readonly IWorkLane _lane;
    private bool _available;
    private string _status = "";
    private int _busy;

    /// <param name="lane">Verilmezse her şey çağıranın iş parçacığında hemen çalışır (testler).</param>
    public DdcPanelViewModel(IDdcPanel panel, IWorkLane? lane = null)
    {
        _panel = panel;
        _lane = lane ?? WorkLane.Inline;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Giriş başka porta alınırken kullanıcıya sorulur (MessageBox); verilmezse reddedilir.</summary>
    public Func<string, string, bool>? ConfirmInputSwitch { get; set; }

    public ObservableCollection<PanelItemViewModel> Items { get; } = [];

    public bool Available
    {
        get => _available;
        private set
        {
            _available = value;
            Raise();
            Raise(nameof(ShowOsdInstructions));
        }
    }

    public bool ShowOsdInstructions => !_available && !IsBusy;

    /// <summary>Monitörle konuşuluyor: kaydırıcılar kapalı, "okunuyor" yazısı görünür.</summary>
    public bool IsBusy => _busy > 0;

    public bool IsReady => !IsBusy;

    public string Status
    {
        get => _status;
        private set
        {
            _status = value;
            Raise();
        }
    }

    public string OsdText { get; } = string.Join(Environment.NewLine + Environment.NewLine,
        new[] { "Why", "Brightness", "Color", "Image", "Input", "Volume" }.Select(k => Loc.Get("DdcPanel.Osd_" + k)));

    public Task Refresh() => Busy(Load);

    /// <summary>Kaydırıcı bırakıldı ya da seçim yapıldı: tek yazma.</summary>
    public Task Commit(PanelItemViewModel item) => Busy(async () =>
    {
        var (setting, value) = (item.Setting, item.ValueToWrite);
        var result = await _lane.RunAsync(() => _panel.Set(setting, value));
        if (result == PanelSetResult.NeedsInputWarning)
        {
            var accepted = ConfirmInputSwitch?.Invoke(Loc.Get("DdcPanel.InputWarning_Title"), Loc.Get("DdcPanel.InputWarning_Text")) == true;
            if (accepted) result = await _lane.RunAsync(() => _panel.Set(setting, value, inputWarningAccepted: true));
        }

        // Aynı bırakma olayı iki kez gelirse (sürükleme + fare) "zaten bu" mesajı gerçek sonucun üstüne yazılmaz.
        if (result != PanelSetResult.Unchanged) Status = Loc.Get("DdcPanel.Result_" + result);
        await Load();
    });

    /// <summary>Monitörden okur. OSD'den değişen başka ayar varsa söyler (değer zaten alındı, ezilmedi).</summary>
    private async Task Load()
    {
        var state = await _lane.RunAsync(_panel.Refresh);
        Items.Clear();
        foreach (var setting in state.Settings) Items.Add(new PanelItemViewModel(setting, item => Commit(item).Forget()));
        Available = state.Available;

        if (state.ExternallyChanged.Count > 0)
        {
            var names = string.Join(", ", state.ExternallyChanged.Select(s => Loc.Get("DdcPanel.Setting_" + s)));
            Status = string.Format(CultureInfo.CurrentCulture, Loc.Get("DdcPanel.ExternalChange"), names);
        }
    }

    private async Task Busy(Func<Task> work)
    {
        _busy++;
        RaiseBusy();
        try
        {
            await work();
        }
        finally
        {
            _busy--;
            RaiseBusy();
        }
    }

    private void RaiseBusy()
    {
        Raise(nameof(IsBusy));
        Raise(nameof(IsReady));
        Raise(nameof(ShowOsdInstructions));
    }

    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
