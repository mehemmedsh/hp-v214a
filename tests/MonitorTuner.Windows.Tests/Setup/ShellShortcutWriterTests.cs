using System.IO;
using MonitorTuner.Setup;

namespace MonitorTuner.Windows.Tests.Setup;

/// <summary>Kurulumun kısayolu (T317): Windows kabuğunun okuyabildiği bir .lnk, hedefi uygulama.</summary>
public class ShellShortcutWriterTests
{
    [Fact]
    public void Write_CreatesALinkTheShellResolvesToTheTarget()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mt-lnk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var target = Path.Combine(dir, InstallPaths.AppExeName);
            File.WriteAllBytes(target, []);
            var link = Path.Combine(dir, InstallPaths.ShortcutName);

            new ShellShortcutWriter().Write(link, target, dir, Installer.Description);

            Assert.True(File.Exists(link));
            Assert.Equal(target, ShellShortcutWriter.ReadTarget(link), ignoreCase: true);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
