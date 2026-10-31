using System.Reflection;
using System.Runtime.CompilerServices;
using MonitorTuner.Hardware.Abstractions;

namespace MonitorTuner.Tests.Contracts;

/// <summary>Donanım yazma metotları geri alma noktası ister; geri alma noktasını sadece Güvenlik oluşturur.</summary>
public class HardwareWriteContractTests
{
    private static readonly Assembly Abstractions = typeof(RestorePoint).Assembly;
    private static readonly string[] ReadVerbs = ["Read", "Get", "List", "Enumerate"];

    private static IEnumerable<Type> HardwareInterfaces() =>
        Abstractions.GetExportedTypes().Where(t => t.IsInterface);

    private static IEnumerable<MethodInfo> Methods(Type type) =>
        type.GetMethods().Where(m => !m.IsSpecialName);

    [Fact]
    public void RestorePoint_HasNoPublicConstructorOrFactory()
    {
        var type = typeof(RestorePoint);
        Assert.True(type.IsSealed);
        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.All(type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance), c => Assert.True(c.IsAssembly));
        var factories = Abstractions.GetExportedTypes().SelectMany(t => t.GetMethods())
            .Where(m => m.ReturnType == type);
        Assert.Empty(factories);
    }

    [Fact]
    public void OnlySafetyAndTests_SeeAbstractionsInternals()
    {
        var friends = Abstractions.GetCustomAttributes<InternalsVisibleToAttribute>().Select(a => a.AssemblyName).Order();
        Assert.Equal(["MonitorTuner.Safety", "MonitorTuner.Tests"], friends);
    }

    [Fact]
    public void EveryHardwareWriteMethod_RequiresRestorePoint()
    {
        var writes = HardwareInterfaces().SelectMany(Methods).Where(m => !ReadVerbs.Any(m.Name.StartsWith)).ToList();

        Assert.NotEmpty(writes);
        Assert.All(writes, m => Assert.True(
            m.GetParameters().Any(p => p.ParameterType == typeof(RestorePoint)), $"{m.DeclaringType!.Name}.{m.Name}"));
    }

    [Fact]
    public void HardwareReadMethods_TakeNoRestorePoint_AndInterfacesHaveNoSetters()
    {
        var reads = HardwareInterfaces().SelectMany(Methods).Where(m => ReadVerbs.Any(m.Name.StartsWith));
        Assert.All(reads, m => Assert.DoesNotContain(m.GetParameters(), p => p.ParameterType == typeof(RestorePoint)));
        Assert.All(HardwareInterfaces().SelectMany(t => t.GetProperties()), p => Assert.False(p.CanWrite, p.Name));
    }

    [Fact]
    public void HardwareInterfaces_HaveNoForbiddenOperations()
    {
        // 03-GUVENLIK.md: firmware/EDID yazma yok, Hz modu eklenmez/silinmez, güç/saat/voltaj/fan yok.
        string[] verbs = ["Add", "Remove", "Delete", "Create"];
        string[] nouns = ["Firmware", "Edid", "Power", "Clock", "Voltage", "Fan", "Overclock"];
        var bad = HardwareInterfaces().SelectMany(Methods)
            .Where(m => verbs.Any(m.Name.StartsWith) || nouns.Any(n => m.Name.Contains(n, StringComparison.OrdinalIgnoreCase)))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}");
        Assert.Empty(bad);
    }

    [Fact]
    public void GpuTableWrite_IsMarkedOnlyColorPipelineMayCall()
    {
        var write = typeof(IGpuGammaTable).GetMethod(nameof(IGpuGammaTable.Write))!;
        Assert.NotNull(write.GetCustomAttribute<OnlyColorPipelineMayCallAttribute>());
        var marked = HardwareInterfaces().SelectMany(Methods).Where(m => m.IsDefined(typeof(OnlyColorPipelineMayCallAttribute)));
        Assert.Equal([write], marked);
    }
}
