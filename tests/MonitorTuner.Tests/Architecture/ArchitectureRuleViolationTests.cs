using R = MonitorTuner.Tests.Architecture.ArchitectureRules;

namespace MonitorTuner.Tests.Architecture;

/// <summary>
/// Gerçek derlemeler şimdilik boş olduğu için kurallar doğal olarak geçer. Bu testler, kuralları
/// bilerek çiğneyen MonitorTuner.ArchitectureFixtures derlemesinde ihlallerin yakalandığını kanıtlar.
/// </summary>
public class ArchitectureRuleViolationTests
{
    private const string FixtureNamespace = "MonitorTuner.ArchitectureFixtures";
    private static readonly string Fixture = R.AssemblyPath(FixtureNamespace);

    [Fact]
    public void ForbiddenReference_IsCaught()
    {
        // Core'un kuralı örneğe uygulanıyor: örnek Hardware.Abstractions'a bağlı.
        var found = R.FindForbiddenReferences(Fixture, R.ForbiddenReferences[R.Core]);

        Assert.Equal([R.HardwareAbstractions], found);
    }

    [Fact]
    public void HardwareOnlyNativeCalls_AreCaught_AndHarmlessOnesAreNot()
    {
        var found = R.FindHardwareOnlyNativeCalls(Fixture);

        Assert.Contains(found, f => f.Contains("gdi32.dll!SetDeviceGammaRamp"));
        Assert.Contains(found, f => f.Contains("dxva2.dll!SetVCPFeature"));
        Assert.Contains(found, f => f.Contains("user32.dll!ChangeDisplaySettingsExW"));
        Assert.Contains(found, f => f.Contains("NativeLibrary.Load"));
        Assert.DoesNotContain(found, f => f.Contains("MessageBeep"));
    }

    [Fact]
    public void GpuTableWriterCalls_OutsideAllowedType_AreCaught()
    {
        var found = R.FindRestrictedCalls([Fixture], $"{FixtureNamespace}.AllowedCaller");

        Assert.Contains(found, f => f.StartsWith($"{FixtureNamespace}.SneakyInterfaceCaller.") && f.Contains("IFakeGammaWriter::Write/1"));
        Assert.Contains(found, f => f.StartsWith($"{FixtureNamespace}.SneakyConcreteCaller.") && f.Contains("FakeGammaWriter::Write/1"));
        Assert.DoesNotContain(found, f => f.Contains("AllowedCaller"));
        Assert.DoesNotContain(found, f => f.Contains("::Read/"));
        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void GpuTableWriterCalls_FromColorPipelineName_AreNotAllowedInFixture()
    {
        // Gerçek izinli tip adıyla örnekte izinli çağıran yok: üç çağrının hepsi yakalanır.
        var found = R.FindRestrictedCalls([Fixture], R.ColorPipelineType);

        Assert.Equal(3, found.Count);
    }
}
