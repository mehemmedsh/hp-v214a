using System.IO;
using System.Linq;
using Mono.Cecil;

namespace MonitorTuner.Tests.Architecture;

/// <summary>
/// Mimari kuralları (docs/02-MIMARI.md → CI korumaları). Derlenmiş derlemelerin meta verisini
/// Mono.Cecil ile okur, derlemeleri yüklemez; Windows derlemeleri de bu yüzden incelenebilir.
/// Platformdan bağımsız ve Windows test projeleri bu dosyayı ortak kullanır.
/// </summary>
internal static class ArchitectureRules
{
    public const string Core = "MonitorTuner.Core";
    public const string Color = "MonitorTuner.Color";
    public const string HardwareAbstractions = "MonitorTuner.Hardware.Abstractions";
    public const string Hardware = "MonitorTuner.Hardware";
    public const string Safety = "MonitorTuner.Safety";
    public const string Services = "MonitorTuner.Services";
    public const string Composition = "MonitorTuner.Composition";
    public const string App = "MonitorTuner.App";

    /// <summary>GPU renk tablosuna yazan üyeleri çağırabilecek tek tip.</summary>
    public const string ColorPipelineType = "MonitorTuner.Services.ColorPipeline";

    public const string RestrictedAttribute =
        "MonitorTuner.Hardware.Abstractions.OnlyColorPipelineMayCallAttribute";

    /// <summary>Katman tablosu: her derleme hangi derlemelere bağlı olamaz.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> ForbiddenReferences =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [Core] = [HardwareAbstractions, Hardware, Safety, Services, Composition, App],
            [Color] = [HardwareAbstractions, Hardware, Safety, Services, Composition, App],
            [HardwareAbstractions] = [Hardware, Safety, Services, Composition, App],
            [Safety] = [Hardware, Services, Composition, App],
            [Services] = [Hardware, Composition, App],
            [Hardware] = [Safety, Services, Composition, App],
            [Composition] = [App],
            [App] = [HardwareAbstractions, Hardware],
        };

    // Sadece Hardware'in çağırabileceği yerel (P/Invoke) fonksiyonlar: DDC, NVIDIA, gamma tablosu, ekran modu.
    private static readonly string[] HardwareOnlyModules = ["dxva2", "nvapi", "nvapi64"];
    private static readonly string[] HardwareOnlyFunctions =
        ["SetDeviceGammaRamp", "GetDeviceGammaRamp", "ChangeDisplaySettings", "ChangeDisplaySettingsEx", "SetDisplayConfig"];

    public static string AssemblyPath(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, name + ".dll");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Derleme test çıktısında yok: {path}");
        return path;
    }

    public static IReadOnlyList<string> FindForbiddenReferences(string assemblyPath, IEnumerable<string> forbidden)
    {
        var set = forbidden.ToHashSet(StringComparer.Ordinal);
        using var module = ModuleDefinition.ReadModule(assemblyPath);
        return module.AssemblyReferences.Select(r => r.Name).Where(set.Contains)
            .Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>Hardware dışında olmaması gereken P/Invoke ve NativeLibrary kullanımlarını listeler.</summary>
    public static IReadOnlyList<string> FindHardwareOnlyNativeCalls(string assemblyPath)
    {
        using var module = ModuleDefinition.ReadModule(assemblyPath);
        var found = new List<string>();
        foreach (var type in module.GetTypes())
        foreach (var method in type.Methods)
        {
            if (method.IsPInvokeImpl && method.PInvokeInfo is { } info)
            {
                var dll = Path.GetFileNameWithoutExtension(info.Module.Name).ToLowerInvariant();
                var entry = string.IsNullOrEmpty(info.EntryPoint) ? method.Name : info.EntryPoint;
                if (HardwareOnlyModules.Contains(dll) || IsHardwareOnlyFunction(entry))
                    found.Add($"{type.FullName}.{method.Name} → {info.Module.Name}!{entry}");
            }

            if (!method.HasBody) continue;
            foreach (var instruction in method.Body.Instructions)
                if (instruction.Operand is MemberReference
                    { DeclaringType.FullName: "System.Runtime.InteropServices.NativeLibrary" } member)
                    found.Add($"{type.FullName}.{method.Name} → NativeLibrary.{member.Name}");
        }
        return found;
    }

    /// <summary>
    /// [OnlyColorPipelineMayCall] ile işaretli arayüz üyelerini (ve onları uygulayan sınıf metotlarını)
    /// izin verilen tip ve onun iç tipleri (lambda, async) dışında çağıran yerleri listeler.
    /// </summary>
    public static IReadOnlyList<string> FindRestrictedCalls(IEnumerable<string> assemblyPaths, string allowedType)
    {
        var modules = assemblyPaths.Select(p => ModuleDefinition.ReadModule(p)).ToList();
        try
        {
            var types = modules.SelectMany(m => m.GetTypes()).ToList();
            var restricted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var type in types)
            foreach (var method in type.Methods)
                if (method.CustomAttributes.Any(a => a.AttributeType.FullName == RestrictedAttribute))
                    restricted.Add(Key(method));

            // Somut sınıf üzerinden yapılan çağrı da yakalansın: uygulayan metotlar da kısıtlı.
            foreach (var type in types)
            foreach (var implemented in type.Interfaces)
            {
                var iface = implemented.InterfaceType.GetElementType().FullName;
                foreach (var method in type.Methods)
                    if (restricted.Contains(KeyFor(iface, method)) || method.Overrides.Any(o => restricted.Contains(Key(o))))
                        restricted.Add(Key(method));
            }

            var found = new List<string>();
            foreach (var type in types.Where(t => !IsWithin(t, allowedType)))
            foreach (var method in type.Methods.Where(m => m.HasBody))
            foreach (var instruction in method.Body.Instructions)
                if (instruction.Operand is MethodReference called && restricted.Contains(Key(called)))
                    found.Add($"{type.FullName}.{method.Name} → {Key(called)}");
            return found;
        }
        finally
        {
            modules.ForEach(m => m.Dispose());
        }
    }

    private static bool IsHardwareOnlyFunction(string entry) =>
        HardwareOnlyFunctions.Any(f => entry == f || entry == f + "W" || entry == f + "A");

    private static bool IsWithin(TypeDefinition type, string allowedType)
    {
        for (var current = type; current is not null; current = current.DeclaringType)
            if (current.FullName == allowedType) return true;
        return false;
    }

    private static string Key(MethodReference method) => KeyFor(method.DeclaringType.GetElementType().FullName, method);

    private static string KeyFor(string typeName, MethodReference method) =>
        $"{typeName}::{method.Name}/{method.Parameters.Count}";
}
