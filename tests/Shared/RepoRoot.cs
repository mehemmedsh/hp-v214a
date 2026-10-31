using System.IO;

namespace MonitorTuner.Tests;

/// <summary>Test çıktısından yukarı çıkarak çözüm dosyasının olduğu repo kökünü bulur.</summary>
internal static class RepoRoot
{
    public static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "MonitorTuner.slnx")))
                return dir.FullName;
        throw new DirectoryNotFoundException("Repo kökü (MonitorTuner.slnx) bulunamadı.");
    }
}
