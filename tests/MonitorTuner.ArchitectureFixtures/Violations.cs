using System.Runtime.InteropServices;
using MonitorTuner.Hardware.Abstractions;

namespace MonitorTuner.ArchitectureFixtures;

// Bu derleme mimari kurallarını BİLEREK çiğner; uygulamaya dahil değildir.
// Mimari testleri (ArchitectureRuleViolationTests) buradaki ihlalleri yakaladığını kanıtlar.

public interface IFakeGammaWriter
{
    [OnlyColorPipelineMayCall]
    void Write(ushort[] ramp);

    ushort[] Read();
}

public sealed class FakeGammaWriter : IFakeGammaWriter
{
    public void Write(ushort[] ramp) { }

    public ushort[] Read() => new ushort[768];
}

/// <summary>İzinli çağıran (testte izinli tip olarak verilir); lambda içinden çağrı da izinli sayılmalı.</summary>
public sealed class AllowedCaller
{
    public void Apply(IFakeGammaWriter writer)
    {
        Action write = () => writer.Write(writer.Read());
        write();
    }
}

/// <summary>İhlal: arayüz üzerinden yazma.</summary>
public sealed class SneakyInterfaceCaller
{
    public void Apply(IFakeGammaWriter writer) => writer.Write(writer.Read());
}

/// <summary>İhlal: somut sınıf üzerinden yazma.</summary>
public sealed class SneakyConcreteCaller
{
    public void Apply(FakeGammaWriter writer) => writer.Write(new ushort[768]);
}

/// <summary>İhlal: Hardware dışında gamma, DDC ve ekran modu P/Invoke'u; MessageBeep zararsız örnektir.</summary>
public static class ForbiddenNative
{
    [DllImport("gdi32.dll")]
    public static extern bool SetDeviceGammaRamp(nint hdc, ushort[] ramp);

    [DllImport("dxva2.dll")]
    public static extern bool SetVCPFeature(nint monitor, byte code, uint value);

    [DllImport("user32.dll", EntryPoint = "ChangeDisplaySettingsExW")]
    public static extern int ChangeDisplayMode(nint deviceName, nint mode, nint hwnd, uint flags, nint param);

    [DllImport("user32.dll")]
    public static extern bool MessageBeep(uint type);

    public static nint LoadNvapi() => NativeLibrary.Load("nvapi64.dll");
}
