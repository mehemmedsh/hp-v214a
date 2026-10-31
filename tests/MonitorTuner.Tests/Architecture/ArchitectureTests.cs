using R = MonitorTuner.Tests.Architecture.ArchitectureRules;

namespace MonitorTuner.Tests.Architecture;

/// <summary>Platformdan bağımsız derlemelerin mimari kuralları (Linux'ta da çalışır).</summary>
public class ArchitectureTests
{
    // Not: "R." öneki şart; yoksa Core, Color gibi adlar T002'den sonra MonitorTuner.Core vb. ad alanlarıyla çakışır.
    private static readonly string[] Portable = [R.Core, R.Color, R.HardwareAbstractions, R.Safety, R.Services];

    public static TheoryData<string> PortableAssemblies()
    {
        var data = new TheoryData<string>();
        foreach (var name in Portable) data.Add(name);
        return data;
    }

    [Theory]
    [MemberData(nameof(PortableAssemblies))]
    public void Assembly_DoesNotReferenceForbiddenLayers(string name) =>
        Assert.Empty(R.FindForbiddenReferences(R.AssemblyPath(name), R.ForbiddenReferences[name]));

    [Theory]
    [MemberData(nameof(PortableAssemblies))]
    public void Assembly_HasNoHardwareOnlyNativeCalls(string name) =>
        Assert.Empty(R.FindHardwareOnlyNativeCalls(R.AssemblyPath(name)));

    [Fact]
    public void OnlyColorPipeline_CallsGpuTableWriters() =>
        Assert.Empty(R.FindRestrictedCalls(Portable.Select(R.AssemblyPath), R.ColorPipelineType));

    [Fact]
    public void LayerTable_CoversEverySourceProject()
    {
        var projects = Directory.GetDirectories(Path.Combine(RepoRoot.Find(), "src"), "MonitorTuner.*")
            .Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(8, projects.Count);
        Assert.Equal(R.ForbiddenReferences.Keys.Order(StringComparer.Ordinal), projects);
    }
}
