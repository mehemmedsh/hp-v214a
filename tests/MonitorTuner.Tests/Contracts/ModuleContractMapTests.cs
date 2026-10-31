using System.Text.RegularExpressions;
using MonitorTuner.Color;
using MonitorTuner.Core.Profiles;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;
using MonitorTuner.Services;

namespace MonitorTuner.Tests.Contracts;

/// <summary>02-MIMARI.md → Modül sözleşmeleri tablosundaki her modülün kodda bir arayüzü var.</summary>
public class ModuleContractMapTests
{
    public static readonly IReadOnlyDictionary<string, Type[]> Modules = new Dictionary<string, Type[]>
    {
        ["Monitör tanıma"] = [typeof(IMonitorIdentifier), typeof(IDisplayEnumerator)],
        ["Monitör profili"] = [typeof(IMonitorProfileReader)],
        ["Ayar deposu"] = [typeof(ISettingsStore)],
        ["Güvenlik katmanı"] = [typeof(ISafetyLayer), typeof(IGpuTableOwner)],
        ["DDC katmanı"] = [typeof(IDdcControl), typeof(IDdcMonitor)],
        ["Windows ekran katmanı"] = [typeof(IDisplayModes)],
        ["Hz paneli"] = [typeof(IRefreshRatePanel)],
        ["Renk boru hattı"] = [typeof(IColorPipeline), typeof(IGpuGammaTable), typeof(IGdiIcmGammaRange), typeof(IGammaTableCombiner)],
        ["NVIDIA katmanı"] = [typeof(INvidiaSettings)],
        ["Kelvin hesaplayıcı"] = [typeof(IKelvinCalculator)],
        ["Karartma planlayıcı"] = [typeof(IDimmingPlanner)],
        ["Gözle kalibrasyon"] = [typeof(ICalibrationCurves)],
        ["Renk profilleri"] = [typeof(IColorProfiles)],
        ["Ayar sihirbazı"] = [typeof(ISetupWizard)],
        ["Kurtarma"] = [typeof(IRecovery)],
    };

    [Fact]
    public void EveryModuleInArchitectureDoc_HasContract()
    {
        var doc = File.ReadAllText(Path.Combine(RepoRoot.Find(), "docs", "02-MIMARI.md"));
        var section = doc[doc.IndexOf("## Modül sözleşmeleri", StringComparison.Ordinal)..];
        section = section[..section.IndexOf("\n## ", 5, StringComparison.Ordinal)];
        var names = Regex.Matches(section, @"^\| \*\*(.+?)\*\* \|", RegexOptions.Multiline).Select(m => m.Groups[1].Value);

        Assert.Equal(names.Order(), Modules.Keys.Order());
    }

    [Fact]
    public void EveryContract_IsAPublicInterface() =>
        Assert.All(Modules.Values.SelectMany(t => t), t => Assert.True(t.IsInterface && t.IsPublic, t.Name));

    [Fact]
    public void ColorPipeline_ImplementsPipelineAndTableOwner()
    {
        Assert.Equal("MonitorTuner.Services.ColorPipeline", typeof(ColorPipeline).FullName);
        Assert.True(typeof(IColorPipeline).IsAssignableFrom(typeof(ColorPipeline)));
        Assert.True(typeof(IGpuTableOwner).IsAssignableFrom(typeof(ColorPipeline)));
    }
}
