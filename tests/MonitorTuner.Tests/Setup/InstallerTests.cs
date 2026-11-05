using MonitorTuner.Setup;

namespace MonitorTuner.Tests.Setup;

/// <summary>
/// Kurulum dosyası (T317): paketteki uygulama ve kurtarma klasörleri kurulur, iki kısayol yazılır; güncelleme eskisini siler;
/// kaldırma kısayolları ve klasörü siler; bizim olmayan klasöre dokunulmaz. Geçici klasörlerde, sahte kısayol yazıcıyla.
/// </summary>
public sealed class InstallerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mt-kurulum-" + Guid.NewGuid().ToString("N"));
    private readonly FakeShortcuts _shortcuts = new();
    private readonly InstallPaths _paths;
    private readonly Installer _installer;

    public InstallerTests()
    {
        _paths = new InstallPaths(Path.Combine(_root, "paket"), Path.Combine(_root, "Programs", "MonitorTuner"),
            Path.Combine(_root, "Masaüstü"), Path.Combine(_root, "Başlat"));
        _installer = new Installer(_paths, _shortcuts);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Install_CopiesAppAndRecovery_WritesBothShortcutsToTheApp()
    {
        Package("1");

        _installer.Install();

        Assert.Equal(InstallState.Installed, _installer.State);
        Assert.Equal("1", File.ReadAllText(_paths.AppExe));
        Assert.Equal("dil", File.ReadAllText(Path.Combine(_paths.Target, "tr", "MonitorTuner.App.resources.dll")));
        Assert.True(File.Exists(Path.Combine(_paths.Target, "kurtarma", "MonitorTuner-Kurtarma.exe")));
        Assert.Equal(
            [(_paths.DesktopShortcut, _paths.AppExe, _paths.Target), (_paths.StartMenuShortcut, _paths.AppExe, _paths.Target)],
            _shortcuts.Written.Select(w => (w.Link, w.Target, w.WorkingDirectory)));
        Assert.EndsWith("MonitorTuner.lnk", _paths.DesktopShortcut, StringComparison.Ordinal);
    }

    [Fact]
    public void Install_Again_ReplacesTheOldVersion_AndLeavesNoOldFiles()
    {
        Package("1");
        _installer.Install();
        File.WriteAllText(Path.Combine(_paths.Target, "eski.dll"), "eski");
        Package("2");

        _installer.Install();

        Assert.Equal("2", File.ReadAllText(_paths.AppExe));
        Assert.False(File.Exists(Path.Combine(_paths.Target, "eski.dll")));
    }

    [Fact]
    public void Install_IncompletePackage_Refuses_AndWritesNothing()
    {
        Directory.CreateDirectory(_paths.Package);

        var ex = Assert.Throws<InstallException>(_installer.Install);

        Assert.Contains("Paket eksik", ex.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(_paths.Target));
        Assert.Empty(_shortcuts.Written);
    }

    [Fact]
    public void ForeignFolder_IsNeverDeleted_ByInstallOrUninstall()
    {
        Package("1");
        Directory.CreateDirectory(_paths.Target);
        var mine = Path.Combine(_paths.Target, "başkasının.txt");
        File.WriteAllText(mine, "dokunma");

        Assert.Equal(InstallState.ForeignFolder, _installer.State);
        Assert.Throws<InstallException>(_installer.Install);
        Assert.Throws<InstallException>(_installer.Uninstall);
        Assert.Equal("dokunma", File.ReadAllText(mine));
    }

    [Fact]
    public void Uninstall_RemovesShortcutsAndFolder_KeepsOtherDesktopFiles()
    {
        Package("1");
        _installer.Install();
        var other = Path.Combine(_paths.Desktop, "not.txt");
        File.WriteAllText(other, "kalır");

        _installer.Uninstall();

        Assert.Equal(InstallState.NotInstalled, _installer.State);
        Assert.False(Directory.Exists(_paths.Target));
        Assert.False(File.Exists(_paths.DesktopShortcut));
        Assert.False(File.Exists(_paths.StartMenuShortcut));
        Assert.True(File.Exists(other));
    }

    [Fact]
    public void State_EmptyTargetFolder_CountsAsNotInstalled()
    {
        Directory.CreateDirectory(_paths.Target);

        Assert.Equal(InstallState.NotInstalled, _installer.State);
        Assert.False(_installer.PackageIsComplete);
    }

    private void Package(string version)
    {
        var app = Path.Combine(_paths.Package, InstallPaths.AppFolder);
        Directory.CreateDirectory(Path.Combine(app, "tr"));
        File.WriteAllText(Path.Combine(app, InstallPaths.AppExeName), version);
        File.WriteAllText(Path.Combine(app, "tr", "MonitorTuner.App.resources.dll"), "dil");
        var recovery = Path.Combine(_paths.Package, InstallPaths.RecoveryFolder);
        Directory.CreateDirectory(recovery);
        File.WriteAllText(Path.Combine(recovery, "MonitorTuner-Kurtarma.exe"), "k");
    }

    private sealed class FakeShortcuts : IShortcutWriter
    {
        public List<(string Link, string Target, string WorkingDirectory)> Written { get; } = [];

        public void Write(string linkPath, string targetPath, string workingDirectory, string description)
        {
            Written.Add((linkPath, targetPath, workingDirectory));
            File.WriteAllText(linkPath, targetPath);
        }
    }
}
