using R = MonitorTuner.Tests.Architecture.ArchitectureRules;

namespace MonitorTuner.Windows.Tests.Architecture;

/// <summary>Windows derlemeleri dahil bütün çözümün mimari kuralları.</summary>
public class WindowsArchitectureTests
{
    // Not: "R." öneki şart; yoksa App ve Hardware adları MonitorTuner.App / MonitorTuner.Hardware ad alanlarıyla çakışır.
    private static readonly string[] All =
        [R.Core, R.Color, R.HardwareAbstractions, R.Safety, R.Services, R.Hardware, R.Composition, R.App];

    public static TheoryData<string> WindowsAssemblies() => [R.Hardware, R.Composition, R.App];

    public static TheoryData<string> AllButHardware()
    {
        var data = new TheoryData<string>();
        foreach (var name in All.Where(n => n != R.Hardware)) data.Add(name);
        return data;
    }

    [Theory]
    [MemberData(nameof(WindowsAssemblies))]
    public void Assembly_DoesNotReferenceForbiddenLayers(string name) =>
        Assert.Empty(R.FindForbiddenReferences(R.AssemblyPath(name), R.ForbiddenReferences[name]));

    [Theory]
    [MemberData(nameof(AllButHardware))]
    public void HardwareOnlyNativeCalls_AreOnlyInHardware(string name) =>
        Assert.Empty(R.FindHardwareOnlyNativeCalls(R.AssemblyPath(name)));

    [Fact]
    public void OnlyColorPipeline_CallsGpuTableWriters_AcrossWholeSolution() =>
        Assert.Empty(R.FindRestrictedCalls(All.Select(R.AssemblyPath), R.ColorPipelineType));
}
