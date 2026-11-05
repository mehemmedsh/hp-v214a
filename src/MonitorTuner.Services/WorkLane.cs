namespace MonitorTuner.Services;

/// <summary>
/// Servis çağrılarının nerede çalışacağı (T316). Servisler tek iş parçacığı için yazıldı; DDC okuması ise yavaştır (her kod
/// 50–100 ms, yetenek dizesi birkaç saniye). Arayüz donmasın diye yavaş işler arka planda, ama hepsi tek sıradan geçer:
/// aynı anda iki servis çağrısı hiç çalışmaz. İç içe çağrılmaz (sıradaki iş sırayı yeniden istemez).
/// </summary>
public interface IWorkLane
{
    /// <summary>Arka planda, sırayla. Sonuç (ya da hata) await eden arayüze döner.</summary>
    Task<T> RunAsync<T>(Func<T> work);

    /// <summary>Çağıranın iş parçacığında; arka planda süren iş bitene kadar bekler. Kısa işler içindir (tepsi, kapanış).</summary>
    T Run<T>(Func<T> work);
}

public static class WorkLaneExtensions
{
    public static Task RunAsync(this IWorkLane lane, Action work)
    {
        ArgumentNullException.ThrowIfNull(lane);
        ArgumentNullException.ThrowIfNull(work);
        return lane.RunAsync<object?>(() =>
        {
            work();
            return null;
        });
    }

    public static void Run(this IWorkLane lane, Action work)
    {
        ArgumentNullException.ThrowIfNull(lane);
        ArgumentNullException.ThrowIfNull(work);
        lane.Run<object?>(() =>
        {
            work();
            return null;
        });
    }
}

/// <summary>Uygulamanın sırası: işler iş parçacığı havuzunda, bir semafor ile tek tek çalışır.</summary>
public sealed class WorkLane : IWorkLane
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Her şeyi çağıranın iş parçacığında hemen çalıştırır (testler ve tasarım zamanı): görev tamamlanmış döner.</summary>
    public static IWorkLane Inline { get; } = new InlineLane();

    public Task<T> RunAsync<T>(Func<T> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        // Semafor arka planda alınır ve bırakılır: arayüz Run ile beklerken bırakma arayüz iş parçacığını beklemez (kilitlenme yok).
        return Task.Run(async () =>
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                return work();
            }
            finally
            {
                _gate.Release();
            }
        });
    }

    public T Run<T>(Func<T> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        _gate.Wait();
        try
        {
            return work();
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed class InlineLane : IWorkLane
    {
        public Task<T> RunAsync<T>(Func<T> work)
        {
            ArgumentNullException.ThrowIfNull(work);
            try
            {
                return Task.FromResult(work());
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }
        }

        public T Run<T>(Func<T> work)
        {
            ArgumentNullException.ThrowIfNull(work);
            return work();
        }
    }
}

/// <summary>
/// Tepsinin <see cref="IQuickControls"/>'ü sıradan geçer (T316): arka planda bir DDC okuması sürerken tepsiden parlaklık seçilirse
/// okuma bitince yazılır, ikisi aynı anda çalışmaz. <see cref="Enabled"/> ve <see cref="Profiles"/> sadece okur, sırayı beklemez
/// (menü her açılışta okunur; ayar deposu kendi kilidiyle okunur).
/// </summary>
public sealed class LanedQuickControls(IQuickControls inner, IWorkLane lane) : IQuickControls
{
    public bool Enabled => inner.Enabled;

    public IReadOnlyList<ColorProfile> Profiles => inner.Profiles;

    public QuickResult SelectProfile(string name) => lane.Run(() => inner.SelectProfile(name));

    public QuickResult SetBrightness(double slider) => lane.Run(() => inner.SetBrightness(slider));

    public QuickResult SetBlueLight(int kelvin) => lane.Run(() => inner.SetBlueLight(kelvin));

    public QuickResult Panic() => lane.Run(inner.Panic);
}
