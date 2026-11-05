using MonitorTuner.Services;

namespace MonitorTuner.Tests.Services;

/// <summary>Servis sırası (T316): yavaş işler arka planda ama tek tek; arayüzden kısa iş sırayı bekler; hata çağırana döner.</summary>
public class WorkLaneTests
{
    [Fact]
    public async Task RunAsync_ReturnsBeforeTheWorkIsDone_CallerIsNotBlocked()
    {
        var lane = new WorkLane();
        using var release = new ManualResetEventSlim();

        var task = lane.RunAsync(() =>
        {
            release.Wait(TimeSpan.FromSeconds(5));
            return 42;
        });

        Assert.False(task.IsCompleted); // çağıran (arayüz) beklemeden devam eder
        release.Set();
        Assert.Equal(42, await task);
    }

    [Fact]
    public async Task RunAsync_NeverRunsTwoWorksAtTheSameTime()
    {
        var lane = new WorkLane();
        var running = 0;
        var maxRunning = 0;

        var tasks = Enumerable.Range(0, 20).Select(i => lane.RunAsync(() =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref maxRunning, now);
            Thread.Sleep(5);
            Interlocked.Decrement(ref running);
            return i;
        })).ToList();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, maxRunning);
        Assert.Equal(Enumerable.Range(0, 20), results.Order());
    }

    [Fact]
    public async Task Run_WaitsForTheBackgroundWorkToFinish()
    {
        var lane = new WorkLane();
        using var started = new ManualResetEventSlim();
        var order = new List<string>();

        var background = lane.RunAsync(() =>
        {
            started.Set();
            Thread.Sleep(100);
            lock (order) order.Add("arka plan");
            return 0;
        });
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        lane.Run(() => { lock (order) order.Add("arayüz"); });
        await background;

        Assert.Equal(["arka plan", "arayüz"], order);
    }

    [Fact]
    public async Task RunAsync_ExceptionReachesTheCaller_AndTheLaneKeepsWorking()
    {
        var lane = new WorkLane();

        await Assert.ThrowsAsync<InvalidOperationException>(() => lane.RunAsync<int>(() => throw new InvalidOperationException()));

        Assert.Equal(7, await lane.RunAsync(() => 7));
        Assert.Equal(8, lane.Run(() => 8));
    }

    [Fact]
    public async Task Inline_RunsOnTheCallingThread_AndCompletesImmediately()
    {
        var caller = Environment.CurrentManagedThreadId;

        var task = WorkLane.Inline.RunAsync(() => Environment.CurrentManagedThreadId);

        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal(caller, await task);
        Assert.True(WorkLane.Inline.RunAsync<int>(() => throw new InvalidOperationException()).IsFaulted);
    }

    [Fact]
    public async Task LanedQuickControls_WritesWaitForTheLane_ReadsDoNot()
    {
        var lane = new WorkLane();
        var inner = new RecordingQuick();
        var quick = new LanedQuickControls(inner, lane);
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        var background = lane.RunAsync(() =>
        {
            started.Set();
            release.Wait(TimeSpan.FromSeconds(5));
            inner.Calls.Add("arka plan");
            return 0;
        });
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));

        Assert.True(quick.Enabled); // okuma sırayı beklemez
        Assert.Empty(quick.Profiles);
        var write = Task.Run(() => quick.SetBrightness(0.5));
        await Task.Delay(100);
        Assert.False(write.IsCompleted); // yazma arka plandaki iş bitene kadar bekler
        release.Set();
        await background;
        await write;
        quick.Panic();
        quick.SelectProfile("Gündüz");
        quick.SetBlueLight(5000);

        Assert.Equal(["arka plan", "SetBrightness 0.5", "Panic", "SelectProfile Gündüz", "SetBlueLight 5000"], inner.Calls);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        while ((seen = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, seen) != seen) { }
    }

    private sealed class RecordingQuick : IQuickControls
    {
        private static readonly QuickResult Done = new(QuickOutcome.Done);

        public List<string> Calls { get; } = [];
        public bool Enabled => true;
        public IReadOnlyList<ColorProfile> Profiles => [];

        public QuickResult SelectProfile(string name) => Record("SelectProfile " + name);
        public QuickResult SetBrightness(double slider) => Record("SetBrightness " + slider.ToString(System.Globalization.CultureInfo.InvariantCulture));
        public QuickResult SetBlueLight(int kelvin) => Record("SetBlueLight " + kelvin);
        public QuickResult Panic() => Record("Panic");

        private QuickResult Record(string call)
        {
            lock (Calls) Calls.Add(call);
            return Done;
        }
    }
}
