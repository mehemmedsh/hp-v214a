using MonitorTuner.App.ViewModels;
using MonitorTuner.Services;

namespace MonitorTuner.Windows.Tests.ViewModels;

/// <summary>
/// Hızlı ayarlar (T323): sürüklerken yazı değişir ama hiçbir şey yazılmaz; bırakınca tek yazma, aynı değer tekrar yazılmaz; okuma
/// sadece okur; panik efektleri kapatır ve kaydırıcıları yeniden okur; hatalar kullanıcıya söylenir, uygulama düşmez.
/// </summary>
public class QuickSettingsViewModelTests
{
    private sealed class StubQuick : IQuickControls
    {
        public List<string> Calls { get; } = [];
        public double? Slider { get; set; } = 0.5;
        public int Kelvin { get; set; } = 6500;
        public QuickResult Next { get; set; } = new(QuickOutcome.Done);
        public Exception? Throw { get; set; }

        public bool Enabled => true;
        public IReadOnlyList<ColorProfile> Profiles => [];
        public ActiveMode? Active { get; set; }
        public int BlueLightKelvin => Kelvin;

        public double? ReadBrightness()
        {
            Calls.Add("read");
            if (Throw is { } ex) throw ex;
            return Slider;
        }

        public QuickResult SelectProfile(string name) => throw new NotSupportedException();

        public QuickResult SetBrightness(double slider)
        {
            Calls.Add("brightness:" + slider.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            if (Throw is { } ex) throw ex;
            return Next;
        }

        public QuickResult SetBlueLight(int kelvin)
        {
            Calls.Add("kelvin:" + kelvin);
            return Next;
        }

        public QuickResult Panic()
        {
            Calls.Add("panic");
            Slider = 0.4;
            Kelvin = 6500;
            return Next;
        }
    }

    private static (QuickSettingsViewModel Vm, StubQuick Quick) Make()
    {
        var quick = new StubQuick();
        var vm = new QuickSettingsViewModel(quick);
        vm.Load();
        quick.Calls.Clear();
        return (vm, quick);
    }

    [Fact]
    public void Load_ReadsTheMonitorAndTheFilter_WritesNothing()
    {
        var quick = new StubQuick { Slider = 0.42, Kelvin = 4500 };
        var vm = new QuickSettingsViewModel(quick);

        vm.Load();

        Assert.Equal(["read"], quick.Calls);
        Assert.Equal(0.42, vm.Brightness);
        Assert.Equal("Arka ışık %27", vm.BrightnessText); // planlayıcının değeri: 27,4999… (kayan nokta) → 27; elle hesaplanmaz
        Assert.Equal(4500, vm.Kelvin);
        Assert.Equal("4500K", vm.KelvinText);
        Assert.True(vm.BrightnessAvailable);
    }

    [Fact]
    public void Dragging_OnlyChangesTheText()
    {
        var (vm, quick) = Make();

        vm.Brightness = 0.1;
        vm.Kelvin = 2650; // 100 K adım

        Assert.Empty(quick.Calls);
        Assert.Equal("Ek karartma: beyaz %57", vm.BrightnessText); // planlayıcı 0,575 verir (57,4999… kayan nokta) → 57
        Assert.Equal(2700, vm.Kelvin);
        Assert.Equal("2700K", vm.KelvinText);
    }

    [Fact]
    public void Releasing_WritesOnce_AndTheSameValueIsNotWrittenAgain()
    {
        var (vm, quick) = Make();
        var changes = 0;
        vm.Changed += () => changes++;

        vm.Brightness = 0.7;
        vm.CommitBrightness();
        vm.CommitBrightness(); // sürükleme bitti + fare bırakıldı: iki olay, tek yazma
        vm.Kelvin = 3400;
        vm.CommitKelvin();
        vm.CommitKelvin();

        Assert.Equal(["brightness:0.7", "kelvin:3400"], quick.Calls);
        Assert.Equal(2, changes);
        Assert.Equal("", vm.Status);
    }

    [Fact]
    public void ReleasingWithoutMoving_WritesNothing()
    {
        var (vm, quick) = Make();

        vm.CommitBrightness();
        vm.CommitKelvin();

        Assert.Empty(quick.Calls);
    }

    /// <summary>Yazılamadıysa (DDC sınırı, sihirbaz açık) söylenir; bir sonraki bırakış yeniden dener.</summary>
    [Fact]
    public void FailedOrBlocked_IsReported_AndRetriedOnTheNextRelease()
    {
        var (vm, quick) = Make();
        quick.Next = new QuickResult(QuickOutcome.Failed);

        vm.Brightness = 0.8;
        vm.CommitBrightness();
        Assert.Contains("uygulanamadı", vm.Status);

        quick.Next = new QuickResult(QuickOutcome.Blocked);
        vm.CommitBrightness();
        Assert.Contains("sihirbaz", vm.Status);

        quick.Next = new QuickResult(QuickOutcome.Done);
        vm.CommitBrightness();
        Assert.Equal(["brightness:0.8", "brightness:0.8", "brightness:0.8"], quick.Calls);
        Assert.Equal("", vm.Status);
    }

    [Fact]
    public void UnexpectedError_IsShown_NotThrown()
    {
        var (vm, quick) = Make();
        quick.Throw = new InvalidOperationException("DDC kilitlendi");

        vm.Brightness = 0.3;
        var task = vm.CommitBrightness();

        Assert.True(task.IsCompletedSuccessfully);
        Assert.Contains("DDC kilitlendi", vm.Status);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public void NoDdc_DisablesTheBrightnessSlider()
    {
        var quick = new StubQuick { Slider = null };
        var vm = new QuickSettingsViewModel(quick);

        vm.Load();

        Assert.False(vm.BrightnessAvailable);
    }

    /// <summary>Panik tepsiyle aynı: efektler kapanır, kaydırıcılar ekrandakini gösterir.</summary>
    [Fact]
    public void Panic_TurnsEffectsOff_AndReloadsTheSliders()
    {
        var (vm, quick) = Make();
        var changes = 0;
        vm.Changed += () => changes++;
        vm.Kelvin = 2700;

        vm.Panic();

        Assert.Equal(["panic", "read"], quick.Calls);
        Assert.Contains("Panik", vm.Status);
        Assert.Equal(1, changes);
        Assert.Equal(0.4, vm.Brightness);
        Assert.Equal(6500, vm.Kelvin);
        Assert.Equal("Kapalı (6500K)", vm.KelvinText);
    }

    /// <summary>Okuma da yazma da arka plandaki sırada (T316); sürerken bölüm kapalı.</summary>
    [Fact]
    public void Work_RunsOnTheLane_AndIsBusyUntilDone()
    {
        var quick = new StubQuick();
        var lane = new ManualLane();
        var vm = new QuickSettingsViewModel(quick, lane);

        var load = vm.Load();
        Assert.True(vm.IsBusy);
        Assert.Empty(quick.Calls);

        lane.RunAll();
        Assert.True(load.IsCompleted);
        Assert.False(vm.IsBusy);
        Assert.Equal(["read"], quick.Calls);
    }
}
