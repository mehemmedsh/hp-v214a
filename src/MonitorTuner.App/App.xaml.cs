using System.Windows;
using MonitorTuner.App.Localization;
using MonitorTuner.Composition;
using MonitorTuner.Services;

namespace MonitorTuner.App;

public partial class App : Application
{
    /// <summary>Kurtarma dosyasının (<c>tools/recovery/Kurtarma.cmd</c>) verdiği argüman (T104).</summary>
    public const string RecoveryArgument = "--kurtarma";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains(RecoveryArgument, StringComparer.OrdinalIgnoreCase))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            RunRecovery();
            Shutdown();
            return;
        }
        new MainWindow().Show();
    }

    /// <summary>Kurtarma kipi: ana pencere, tepsi ve servisler açılmaz; sonuç tek bir pencerede gösterilir.</summary>
    private static void RunRecovery()
    {
        string text;
        var ok = false;
        try
        {
            var reports = RecoveryComposition.ForActiveDisplays().Select(r => r.ResetToDefaults()).ToList();
            ok = reports.Count > 0 && reports.All(IsComplete);
            text = reports.Count == 0 ? Loc.Get("Recovery.NoDisplay") : Describe(reports);
        }
        catch (Exception ex)
        {
            text = Loc.Get("Recovery.Error") + Environment.NewLine + ex.Message;
        }
        MessageBox.Show(text, Loc.Get("Recovery.Title"), MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private static string Describe(IReadOnlyList<RecoveryReport> reports)
    {
        string Line(string field, Func<RecoveryReport, bool> done) =>
            Loc.Get($"Recovery.{field}_{(reports.All(done) ? "Done" : "Failed")}");

        var lines = new[]
        {
            Line("GpuTable", r => r.GpuTableReset),
            Line("GammaRange", r => r.GammaRangeRestored),
            Line("Dithering", r => r.DitheringReset),
            Line("Vibrance", r => r.VibranceReset),
        };
        return string.Join(Environment.NewLine, lines) + Environment.NewLine + Environment.NewLine
            + Loc.Get(reports.All(IsComplete) ? "Recovery.AllDone" : "Recovery.SomeFailed");
    }

    private static bool IsComplete(RecoveryReport r) =>
        r is { GpuTableReset: true, GammaRangeRestored: true, DitheringReset: true, VibranceReset: true };
}
