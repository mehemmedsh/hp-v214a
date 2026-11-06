using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using MonitorTuner.App.Localization;
using MonitorTuner.Color;
using MonitorTuner.Services;

namespace MonitorTuner.App.ViewModels;

/// <summary>
/// Modlar sayfasındaki hızlı ayarlar (T323): parlaklık (tek kaydırıcı: arka ışık, 0,2 altı ek karartma), mavi ışık ve panik.
/// Sürüklerken hiçbir şey yazılmaz, sadece yazı değişir; kaydırıcı bırakılınca tek yazma (<see cref="IQuickControls"/>, sırada, T316).
/// Aynı değer tekrar yazılmaz. Sihirbaz açıkken servis "engellendi" der; arayüz bölümü zaten kapatır. Hiçbir şeyi kendiliğinden
/// değiştirmez: <see cref="Load"/> sadece okur.
/// </summary>
public sealed class QuickSettingsViewModel : INotifyPropertyChanged
{
    private static readonly DimmingPlanner Planner = new();

    private readonly IQuickControls _quick;
    private readonly IWorkLane _lane;

    private double _brightness = 0.6;
    private double? _writtenBrightness;
    private bool _brightnessAvailable = true;
    private double _kelvin = KelvinCalculator.OffKelvin;
    private int _writtenKelvin = KelvinCalculator.OffKelvin;
    private string _status = "";
    private int _busy;

    /// <param name="lane">Verilmezse her şey çağıranın iş parçacığında hemen çalışır (testler).</param>
    public QuickSettingsViewModel(IQuickControls quick, IWorkLane? lane = null)
    {
        _quick = quick;
        _lane = lane ?? WorkLane.Inline;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Bir ayar yazıldı (parlaklık, mavi ışık ya da panik): modlar sayfası "ekrandaki mod"u yeniler.</summary>
    public event Action? Changed;

    public double MinKelvin => KelvinCalculator.MinKelvin;

    public double MaxKelvin => KelvinCalculator.OffKelvin;

    /// <summary>Tek kaydırıcı (0–1), 0,01 adım. Sürüklerken sadece yazı değişir.</summary>
    public double Brightness
    {
        get => _brightness;
        set
        {
            _brightness = Math.Round(Math.Clamp(value, 0, 1), 2, MidpointRounding.AwayFromZero);
            Raise();
            Raise(nameof(BrightnessText));
        }
    }

    /// <summary>"Arka ışık %38" ya da 0,2 altında "Ek karartma: beyaz %58" (planlayıcının değeri, elle hesap değil).</summary>
    public string BrightnessText
    {
        get
        {
            var plan = Planner.Plan(_brightness);
            return plan.GpuScale < 1
                ? Format("Modes.Quick_Dimming", (int)Math.Round(plan.GpuScale * 100))
                : Format("Modes.Quick_Backlight", plan.Backlight);
        }
    }

    /// <summary>false: monitörün parlaklığı okunamadı (DDC yok); kaydırıcı kapalı, OSD kullanılır.</summary>
    public bool BrightnessAvailable
    {
        get => _brightnessAvailable;
        private set
        {
            _brightnessAvailable = value;
            Raise();
        }
    }

    /// <summary>Mavi ışık, 100 K adım; 6500 = kapalı.</summary>
    public double Kelvin
    {
        get => _kelvin;
        set
        {
            _kelvin = Math.Clamp(Math.Round(value / 100, MidpointRounding.AwayFromZero) * 100, KelvinCalculator.MinKelvin, KelvinCalculator.OffKelvin);
            Raise();
            Raise(nameof(KelvinText));
        }
    }

    public string KelvinText => (int)_kelvin >= KelvinCalculator.OffKelvin
        ? Loc.Get("Modes.Quick_BlueLightOff")
        : Format("Modes.Quick_Kelvin", (int)_kelvin);

    /// <summary>Son işlemin sorunu (yazılamadı, sihirbaz açık, panik sonucu); başarılı ayarda boş.</summary>
    public string Status
    {
        get => _status;
        private set
        {
            _status = value;
            Raise();
        }
    }

    public bool IsBusy => _busy > 0;

    public bool IsReady => !IsBusy;

    /// <summary>Pencere açılınca ve bir mod uygulanınca: kaydırıcılar ekrandaki değere gelir. Sadece okur (DDC okuması sırada).</summary>
    public Task Load() => Busy(LoadValues);

    private async Task LoadValues()
    {
        SetKelvinQuietly(_quick.BlueLightKelvin);
        double? slider;
        try
        {
            slider = await _lane.RunAsync(_quick.ReadBrightness);
        }
        catch (Exception)
        {
            slider = null; // okunamadı: kaydırıcı kapalı kalır, uygulama düşmez
        }

        BrightnessAvailable = slider is not null;
        if (slider is { } value)
        {
            Brightness = value;
            _writtenBrightness = _brightness;
        }
    }

    /// <summary>Parlaklık kaydırıcısı bırakıldı: değer değiştiyse tek yazma.</summary>
    public Task CommitBrightness()
    {
        if (_writtenBrightness == _brightness) return Task.CompletedTask;
        var slider = _brightness;
        return Write(() => _quick.SetBrightness(slider), () => _writtenBrightness = slider);
    }

    /// <summary>Mavi ışık kaydırıcısı bırakıldı: değer değiştiyse tek yazma.</summary>
    public Task CommitKelvin()
    {
        var kelvin = (int)_kelvin;
        if (_writtenKelvin == kelvin) return Task.CompletedTask;
        return Write(() => _quick.SetBlueLight(kelvin), () => _writtenKelvin = kelvin);
    }

    /// <summary>Panik (tepsi ve kısayolla aynı): mavi ışık, ek karartma, ton eğrisi ve canlılık kapanır; kaydırıcılar yeniden okunur.</summary>
    public Task Panic() => Busy(async () =>
    {
        try
        {
            var result = await _lane.RunAsync(_quick.Panic);
            Status = result.Outcome == QuickOutcome.Done
                ? Loc.Get("Tray.PanicDone")
                : Format("Tray.PanicFailed", result.Error ?? "?");
        }
        catch (Exception ex)
        {
            Status = Format("Tray.PanicFailed", ex.Message);
        }

        Changed?.Invoke();
        await LoadValues(); // ek karartma ve mavi ışık kapandı: kaydırıcılar ekrandakini göstersin
    });

    private Task Write(Func<QuickResult> write, Action written) => Busy(async () =>
    {
        try
        {
            var result = await _lane.RunAsync(write);
            if (result.Outcome == QuickOutcome.Done)
            {
                written();
                Status = "";
                Changed?.Invoke();
            }
            else
            {
                Status = Loc.Get(result.Outcome == QuickOutcome.Blocked ? "Tray.Blocked" : "Tray.Failed");
            }
        }
        catch (Exception ex)
        {
            Status = Format("Tray.Unexpected", ex.Message);
        }
    });

    private void SetKelvinQuietly(int kelvin)
    {
        Kelvin = kelvin;
        _writtenKelvin = (int)_kelvin;
    }

    private async Task Busy(Func<Task> work)
    {
        _busy++;
        Raise(nameof(IsBusy));
        Raise(nameof(IsReady));
        try
        {
            await work();
        }
        finally
        {
            _busy--;
            Raise(nameof(IsBusy));
            Raise(nameof(IsReady));
        }
    }

    private static string Format(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, Loc.Get(key), args);

    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
