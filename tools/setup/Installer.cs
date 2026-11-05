namespace MonitorTuner.Setup;

/// <summary>Kısayol (.lnk) yazar. Windows'ta <see cref="ShellShortcutWriter"/>; testlerde sahte.</summary>
public interface IShortcutWriter
{
    void Write(string linkPath, string targetPath, string workingDirectory, string description);
}

/// <summary>Kurulumun okunabilir hatası: kullanıcıya olduğu gibi gösterilir.</summary>
public sealed class InstallException(string message) : Exception(message);

public enum InstallState
{
    NotInstalled,
    Installed,

    /// <summary>Hedef klasör var, içi dolu ama bizim kurulumumuz değil: dokunulmaz.</summary>
    ForeignFolder,
}

/// <param name="Package">Paket kökü: kurulum dosyasının yanındaki <c>uygulama</c> ve <c>kurtarma</c> klasörleri.</param>
/// <param name="Target">Kurulum klasörü.</param>
/// <param name="Desktop">Masaüstü klasörü.</param>
/// <param name="StartMenu">Başlat menüsünün Programlar klasörü.</param>
public sealed record InstallPaths(string Package, string Target, string Desktop, string StartMenu)
{
    public const string AppFolder = "uygulama";
    public const string RecoveryFolder = "kurtarma";
    public const string AppExeName = "MonitorTuner.App.exe";
    public const string ShortcutName = "MonitorTuner.lnk";

    /// <summary>Kurulum klasörünün bizim olduğunu gösterir; kaldırma ve güncelleme sadece bu dosya varsa klasörü siler.</summary>
    public const string MarkerName = "MonitorTuner.kurulum";

    public string AppExe => Path.Combine(Target, AppExeName);
    public string Marker => Path.Combine(Target, MarkerName);
    public string DesktopShortcut => Path.Combine(Desktop, ShortcutName);
    public string StartMenuShortcut => Path.Combine(StartMenu, ShortcutName);

    /// <summary>Kullanıcıya özel kurulum: yönetici izni gerekmez (%LocalAppData%\Programs\MonitorTuner).</summary>
    public static InstallPaths ForCurrentUser(string package) => new(
        package,
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "MonitorTuner"),
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        Environment.GetFolderPath(Environment.SpecialFolder.Programs));
}

/// <summary>
/// Kurulum ve kaldırma (T317). Sadece dosya kopyalar ve kısayol yazar: kayıt defterine, donanıma ve uygulamanın ayarlarına
/// (%AppData%\MonitorTuner) dokunmaz. Güncelleme: eski kurulum klasörü silinip yenisi kopyalanır. Bizim olmayan klasöre dokunmaz.
/// </summary>
public sealed class Installer(InstallPaths paths, IShortcutWriter shortcuts)
{
    public const string Description = "MonitorTuner: HP P21b G4 renk, parlaklık ve göz konforu";

    public InstallPaths Paths => paths;

    public InstallState State =>
        !Directory.Exists(paths.Target) ? InstallState.NotInstalled
        : File.Exists(paths.Marker) ? InstallState.Installed
        : Directory.EnumerateFileSystemEntries(paths.Target).Any() ? InstallState.ForeignFolder
        : InstallState.NotInstalled;

    public bool PackageIsComplete => File.Exists(Path.Combine(paths.Package, InstallPaths.AppFolder, InstallPaths.AppExeName));

    public void Install()
    {
        if (!PackageIsComplete)
            throw new InstallException($"Paket eksik: \"{InstallPaths.AppFolder}\\{InstallPaths.AppExeName}\" bulunamadı. Zip'in tamamını ayıkla ve kurulum dosyasını oradan çalıştır.");
        RemoveInstalledFolder();

        Directory.CreateDirectory(paths.Target);
        File.WriteAllText(paths.Marker, "MonitorTuner-Kurulum.exe bu klasörü kurdu; kaldırırken silinir.\n"); // önce: yarım kalan kurulum da bizim sayılır
        CopyTree(Path.Combine(paths.Package, InstallPaths.AppFolder), paths.Target);
        var recovery = Path.Combine(paths.Package, InstallPaths.RecoveryFolder);
        if (Directory.Exists(recovery)) CopyTree(recovery, Path.Combine(paths.Target, InstallPaths.RecoveryFolder));

        Directory.CreateDirectory(paths.Desktop);
        Directory.CreateDirectory(paths.StartMenu);
        shortcuts.Write(paths.DesktopShortcut, paths.AppExe, paths.Target, Description);
        shortcuts.Write(paths.StartMenuShortcut, paths.AppExe, paths.Target, Description);
    }

    /// <summary>Kısayolları ve kurulum klasörünü siler. Uygulamanın ayarları ve yedekleri (%AppData%\MonitorTuner) kalır.</summary>
    public void Uninstall()
    {
        if (State == InstallState.ForeignFolder) throw ForeignFolder();
        File.Delete(paths.DesktopShortcut); // yoksa hata vermez
        File.Delete(paths.StartMenuShortcut);
        RemoveInstalledFolder();
    }

    private void RemoveInstalledFolder()
    {
        switch (State)
        {
            case InstallState.ForeignFolder:
                throw ForeignFolder();
            case InstallState.Installed:
                Directory.Delete(paths.Target, recursive: true);
                break;
        }
    }

    private InstallException ForeignFolder() =>
        new($"\"{paths.Target}\" klasörü var ama bu kurulumun değil; dokunulmadı. İçindekileri kontrol edip klasörü kendin sil, sonra tekrar çalıştır.");

    /// <summary>
    /// Dosyalar yeniden yazılır (içerik akışla kopyalanır): kurulan dosyalar indirilen zip'in "internetten geldi" işaretini
    /// taşımaz, her kurulum programında olduğu gibi.
    /// </summary>
    private static void CopyTree(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, dir)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            using var from = File.OpenRead(file);
            using var to = File.Create(Path.Combine(target, Path.GetRelativePath(source, file)));
            from.CopyTo(to);
        }
    }
}
