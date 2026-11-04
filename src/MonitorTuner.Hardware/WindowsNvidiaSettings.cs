using System.Runtime.InteropServices;
using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;

namespace MonitorTuner.Hardware;

/// <summary>
/// NVIDIA dithering ve canlılık (nvapi64, T303). Durum tutmaz; ekran NVIDIA kartında değilse ya da sürücü cevap vermezse
/// <see cref="Read"/> null, yazmalar false. Fonksiyon kimlikleri: NVIDIA/nvapi <c>nvapi_interface.h</c>; dithering ve
/// canlılık belgesiz (NvAPIWrapper <c>FunctionId.cs</c>, novideo_srgb). Canlılık eski DVC arayüzüyle: seviye 0 = %50
/// (sürücü varsayılanı), en yüksek seviye = %100; %50'nin altı (doygunluk azaltma) yazılmaz.
/// </summary>
public sealed class WindowsNvidiaSettings : INvidiaSettings
{
    // Dithering 8 bit, uzamsal sabit desen: 6 bit + FRC panelin kendi zamansal titreşimine ikinci bir zamansal desen eklenmez.
    private const int Bits8 = 1, ModeSpatialStatic = 1;
    private const int StateDefault = 0, StateEnabled = 1, StateDisabled = 2;

    private static readonly Lazy<Api?> Nv = new(Api.Load);

    public NvidiaState? Read(DisplayId display)
    {
        if (Nv.Value is not { } nv || nv.Find(display) is not { } d) return null;
        var dither = new Dither { Version = 0x10018 };
        var dvc = new DvcInfo { Version = 0x10010 };
        if (nv.GetDither(d.DisplayId, ref dither) != 0 || nv.GetDvc(d.Handle, 0, ref dvc) != 0 || dvc.Max <= 0) return null;
        var mode = dither.State switch
        {
            StateEnabled => DitheringMode.On,
            StateDisabled => DitheringMode.Off,
            _ => DitheringMode.DriverDefault,
        };
        return new NvidiaState(mode, 50 + (int)Math.Round(dvc.Current * 50.0 / dvc.Max));
    }

    public bool SetDithering(DisplayId display, DitheringMode mode, RestorePoint restorePoint)
    {
        Check(display, restorePoint);
        var state = mode switch { DitheringMode.On => StateEnabled, DitheringMode.Off => StateDisabled, _ => StateDefault };
        return Nv.Value is { } nv && nv.Find(display) is { } d && nv.SetDither(d.GpuId, d.OutputId, state, Bits8, ModeSpatialStatic) == 0;
    }

    public bool SetVibrance(DisplayId display, int vibrance, RestorePoint restorePoint)
    {
        Check(display, restorePoint);
        if (vibrance is < 50 or > 100 || Nv.Value is not { } nv || nv.Find(display) is not { } d) return false;
        var dvc = new DvcInfo { Version = 0x10010 };
        if (nv.GetDvc(d.Handle, 0, ref dvc) != 0 || dvc.Max <= 0) return false;
        return nv.SetDvc(d.Handle, 0, (int)Math.Round((vibrance - 50) * dvc.Max / 50.0)) == 0;
    }

    public bool ResetToDefaults(DisplayId display, RestorePoint restorePoint)
    {
        Check(display, restorePoint);
        if (Nv.Value is not { } nv || nv.Find(display) is not { } d) return false;
        var dither = nv.SetDither(d.GpuId, d.OutputId, StateDefault, Bits8, ModeSpatialStatic) == 0;
        return nv.SetDvc(d.Handle, 0, 0) == 0 && dither;
    }

    private static void Check(DisplayId display, RestorePoint restorePoint)
    {
        ArgumentNullException.ThrowIfNull(restorePoint);
        if (restorePoint.Request != new WriteRequest(WriteTarget.NvidiaSettings, display))
            throw new ArgumentException("Geri alma noktası bu NVIDIA yazması için değil.", nameof(restorePoint));
    }

    // ---- nvapi ----

    private readonly record struct Target(uint DisplayId, uint GpuId, uint OutputId, IntPtr Handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct Dither
    {
        public uint Version;
        public int State, Bits, Mode;
        public uint BitsCaps, ModeCaps;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DvcInfo
    {
        public uint Version;
        public int Current, Min, Max;
    }

    private delegate int InitializeFn();
    private delegate int DisplayIdByNameFn([MarshalAs(UnmanagedType.LPStr)] string name, out uint displayId);
    private delegate int GpuAndOutputFn(uint displayId, out IntPtr gpu, out uint outputId);
    private delegate int GpuIdFn(IntPtr gpu, out uint gpuId);
    private delegate int DisplayHandleFn([MarshalAs(UnmanagedType.LPStr)] string name, out IntPtr handle);
    private delegate int GetDitherFn(uint displayId, ref Dither dither);
    private delegate int SetDitherFn(uint gpuId, uint outputId, int state, int bits, int mode);
    private delegate int GetDvcFn(IntPtr handle, uint outputId, ref DvcInfo info);
    private delegate int SetDvcFn(IntPtr handle, uint outputId, int level);

    private sealed record Api(
        DisplayIdByNameFn DisplayIdByName, GpuAndOutputFn GpuAndOutput, GpuIdFn GpuId, DisplayHandleFn DisplayHandle,
        GetDitherFn GetDither, SetDitherFn SetDither, GetDvcFn GetDvc, SetDvcFn SetDvc)
    {
        public static Api? Load()
        {
            try
            {
                if (Fn<InitializeFn>(0x0150E828)() != 0) return null;
                return new Api(
                    Fn<DisplayIdByNameFn>(0xAE457190), Fn<GpuAndOutputFn>(0x112BA1A5), Fn<GpuIdFn>(0x6533EA3E),
                    Fn<DisplayHandleFn>(0x35C29134), Fn<GetDitherFn>(0x932AC8FB), Fn<SetDitherFn>(0xDF0DFCDD),
                    Fn<GetDvcFn>(0x4085DE45), Fn<SetDvcFn>(0x172409B4));
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                return null; // NVIDIA sürücüsü yok ya da bu sürümde fonksiyon yok
            }
        }

        /// <summary>Ekranın NVIDIA kimlikleri; ekran NVIDIA kartında değilse null.</summary>
        public Target? Find(DisplayId display)
        {
            if (DisplayIdByName(display.Value, out var id) != 0 || GpuAndOutput(id, out var gpu, out var output) != 0) return null;
            if (GpuId(gpu, out var gpuId) != 0 || DisplayHandle(display.Value, out var handle) != 0) return null;
            return new Target(id, gpuId, output, handle);
        }

        private static T Fn<T>(uint id) where T : Delegate
        {
            var p = QueryInterface(id);
            return p == IntPtr.Zero
                ? throw new EntryPointNotFoundException($"nvapi {id:X8}")
                : Marshal.GetDelegateForFunctionPointer<T>(p);
        }
    }

    [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface")]
    private static extern IntPtr QueryInterface(uint id);
}
