using System.IO;
using MonitorTuner.App.ViewModels;
using MonitorTuner.Services;

namespace MonitorTuner.Windows.Tests.ViewModels;

/// <summary>Ayarlar'daki mola hatırlatıcısı anahtarı (T324): servise yazar, aynı değeri tekrar yazmaz, kayıt hatasını söyler.</summary>
public class BreakReminderViewModelTests
{
    private sealed class StubBreaks : IBreakReminder
    {
        private bool _enabled;
        public int Sets { get; private set; }
        public Exception? Throw { get; set; }

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (Throw is { } ex) throw ex;
                Sets++;
                _enabled = value;
            }
        }

        public bool Tick(TimeSpan idle, bool paused) => throw new NotSupportedException();
    }

    [Fact]
    public void Toggle_WritesThroughTheService_Once()
    {
        var breaks = new StubBreaks();
        var vm = new BreakReminderViewModel(breaks);
        Assert.False(vm.Enabled);

        vm.Enabled = true;
        vm.Enabled = true;

        Assert.True(breaks.Enabled);
        Assert.Equal(1, breaks.Sets);
        Assert.Equal("", vm.Status);
    }

    [Fact]
    public void SaveFailure_IsShown_AndTheSwitchStaysOff()
    {
        var breaks = new StubBreaks { Throw = new IOException("disk dolu") };
        var vm = new BreakReminderViewModel(breaks);

        vm.Enabled = true;

        Assert.False(vm.Enabled);
        Assert.Contains("disk dolu", vm.Status);
    }
}
