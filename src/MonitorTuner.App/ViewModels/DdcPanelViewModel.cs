using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using MonitorTuner.App.Localization;
using MonitorTuner.Services;

namespace MonitorTuner.App.ViewModels;

public sealed record ChoiceItem(int Value, string Title, bool Selectable)
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
            commit(this); // seçim listesinde seçmek "bırakmak" demektir: tek yazma
        }
    }

    internal int ValueToWrite => IsChoice ? _selected?.Value ?? (int)_value : (int)_value;

    private static IReadOnlyList<ChoiceItem> Choices(PanelSettingState s) => [.. s.Choices.Select(c =>
    {
        var name = Loc.Get("DdcPanel.Choice_" + c.Name);
        if (name.StartsWith("[[", StringComparison.Ordinal)) name = c.Name; // bilinmeyen ön ayar değeri: sayı
        return new ChoiceItem(c.Value, c.Selectable ? name : string.Format(CultureInfo.CurrentCulture, Loc.Get("DdcPanel.Choice_Unverified"), name), c.Selectable);
    })];

    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// DDC paneli ekranının mantığı (T305). Donanıma dokunmaz, sadece <see cref="IDdcPanel"/> ile konuşur. DDC yoksa
/// OSD talimatını gösterir; OSD'den değişen değerleri bildirir, ezmez.
/// </summary>
public sealed class DdcPanelViewModel : INotifyPropertyChanged
{
    private readonly IDdcPanel _panel;
    private bool _available;
    private string _status = "";

    public DdcPanelViewModel(IDdcPanel panel) => _panel = panel;

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

    public bool ShowOsdInstructions => !_available;

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

    public void Refresh() => Load();

    /// <summary>Kaydırıcı bırakıldı ya da seçim yapıldı: tek yazma.</summary>
    public void Commit(PanelItemViewModel item)
    {
        var value = item.ValueToWrite;
        var result = _panel.Set(item.Setting, value);
        if (result == PanelSetResult.NeedsInputWarning)
        {
            var accepted = ConfirmInputSwitch?.Invoke(Loc.Get("DdcPanel.InputWarning_Title"), Loc.Get("DdcPanel.InputWarning_Text")) == true;
            if (accepted) result = _panel.Set(item.Setting, value, inputWarningAccepted: true);
        }

        // Aynı bırakma olayı iki kez gelirse (sürükleme + fare) "zaten bu" mesajı gerçek sonucun üstüne yazılmaz.
        if (result != PanelSetResult.Unchanged) Status = Loc.Get("DdcPanel.Result_" + result);
        Load();
    }

    /// <summary>Monitörden okur. OSD'den değişen başka ayar varsa söyler (değer zaten alındı, ezilmedi).</summary>
    private void Load()
    {
        var state = _panel.Refresh();
        Items.Clear();
        foreach (var setting in state.Settings) Items.Add(new PanelItemViewModel(setting, Commit));
        Available = state.Available;

        if (state.ExternallyChanged.Count > 0)
        {
            var names = string.Join(", ", state.ExternallyChanged.Select(s => Loc.Get("DdcPanel.Setting_" + s)));
            Status = string.Format(CultureInfo.CurrentCulture, Loc.Get("DdcPanel.ExternalChange"), names);
        }
    }

    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
