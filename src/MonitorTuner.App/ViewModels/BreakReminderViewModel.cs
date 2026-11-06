using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using MonitorTuner.App.Localization;
using MonitorTuner.Services;

namespace MonitorTuner.App.ViewModels;

/// <summary>Ayarlar sayfasındaki mola hatırlatıcısı anahtarı (T324). Sadece açar/kapatır; hatırlatmayı uygulama zamanlayıcısı yapar.</summary>
public sealed class BreakReminderViewModel(IBreakReminder breaks) : INotifyPropertyChanged
{
    private string _status = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool Enabled
    {
        get => breaks.Enabled;
        set
        {
            if (breaks.Enabled == value) return;
            try
            {
                breaks.Enabled = value;
                Status = "";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Status = string.Format(CultureInfo.CurrentCulture, Loc.Get("Shell.Break_SaveFailed"), ex.Message);
            }

            Raise();
        }
    }

    /// <summary>Kayıt yazılamadıysa nedeni.</summary>
    public string Status
    {
        get => _status;
        private set
        {
            _status = value;
            Raise();
        }
    }

    private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
