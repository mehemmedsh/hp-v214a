namespace MonitorTuner.Services;

public enum RefreshSwitchResult { AwaitingConfirmation, NeedsOutOfSpecWarning, ModeMissing, Failed }

/// <summary>
/// Hz paneli (T209): onaylı modlar arasında sadece kullanıcı isteğiyle geçiş. Her geçiş 15 sn onay bekler;
/// gelmezse önceki moda dönülür (Güvenlik).
/// </summary>
public interface IRefreshRatePanel
{
    /// <summary>Onaylı modlar (76, 75, 72, 60, 50).</summary>
    IReadOnlyList<int> ApprovedRatesHz { get; }

    /// <summary>Onaylı olup Windows listesinde artık olmayanlar (sürücü güncellemesi sildi).</summary>
    IReadOnlyList<int> GetMissingRatesHz();

    /// <param name="outOfSpecWarningAccepted">Spec dışı (72 Hz üstü) ilk geçişte kullanıcı uyarıyı kabul etti mi.</param>
    RefreshSwitchResult SwitchTo(int refreshHz, bool outOfSpecWarningAccepted = false);

    /// <summary>Kullanıcı "görüntü geldi" dedi.</summary>
    void Confirm();

    /// <summary>Monitörün gördüğü Hz (DDC AE). null: okunamadı.</summary>
    int? ReadMonitorRefreshHz();
}
