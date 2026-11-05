using System.IO;
using MonitorTuner.Tests;
using MonitorTuner.Composition;
using MonitorTuner.Core.Profiles;

namespace MonitorTuner.Windows.Tests.Composition;

/// <summary>Bağlama kökünün donanımsız yarısı (T314): gömülü monitör profilleri. Gerçek donanım kurulumu çalıştırılmaz.</summary>
public class AppCompositionTests
{
    [Fact]
    public void LoadProfiles_ReadsTheEmbeddedHpProfile_SameAsTheFileInTheRepo()
    {
        var embedded = Assert.Single(AppComposition.LoadProfiles());
        var onDisk = new MonitorProfileReader().Read(
            File.ReadAllText(Path.Combine(RepoRoot.Find(), "profiles", "hp-p21b-g4.json")));

        Assert.Equal("HP P21b G4", embedded.Model);
        Assert.Equal(onDisk.Modes.Select(m => m.RefreshHz), embedded.Modes.Select(m => m.RefreshHz));
        Assert.Equal(onDisk.WritableDdcCodes, embedded.WritableDdcCodes);
    }

    [Fact]
    public void LoadProfiles_EmbedsEveryProfileFileInTheRepo()
    {
        var files = Directory.GetFiles(Path.Combine(RepoRoot.Find(), "profiles"), "*.json");

        Assert.Equal(files.Length, AppComposition.LoadProfiles().Count);
    }
}
