using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;
using MonitorTuner.Services;
using MonitorTuner.Tests.Fakes;

namespace MonitorTuner.Tests.Contracts;

/// <summary>Renk boru hattı sözleşmesi (02-MIMARI.md): sahte GPU tablosu, sahte kayıt defteri ve gerçek Güvenlik katmanıyla (T302).</summary>
public class ColorPipelineTests
{
    private static readonly DisplayId Display = FakeHardware.Display;
    private static readonly GammaRamp Calibration = Curve(0.9, 1.0, 1.1);
    private static readonly GammaRamp Warm = GammaRamps.Scale(new RgbGains(1, 0.8, 0.55));
    private static readonly GammaRamp Dim = GammaRamps.Scale(new RgbGains(0.4, 0.4, 0.4));
    private static readonly GammaRamp NightLight = GammaRamps.Scale(new RgbGains(1, 0.7, 0.3));
    private static readonly GammaTableCombiner Combiner = new();

    /// <summary>Katmanlar kalibrasyon → mavi ışık → ek karartma sırasıyla tek tabloya birleşir.</summary>
    [Fact]
    public void Apply_CombinesLayersInOrder_CalibrationBlueLightDimming()
    {
        var rig = new Rig();
        rig.Pipeline.SetLayer(ColorLayer.Dimming, Dim); // çağrı sırası önemli değil, katman sırası önemli
        rig.Pipeline.SetLayer(ColorLayer.BlueLight, Warm);
        rig.Pipeline.SetLayer(ColorLayer.Calibration, Calibration);

        Assert.True(rig.Pipeline.Apply());

        var expected = Combiner.Combine([Calibration, Warm, Dim]);
        Assert.Equal(expected, rig.Hw.Gpu.Read(Display));
        Assert.NotEqual(Combiner.Combine([Dim, Warm, Calibration]), expected);
        var (_, point) = Assert.Single(rig.Hw.Gpu.Writes);
        Assert.Equal(new WriteRequest(WriteTarget.GpuGammaTable, Display), point.Request);

        // Kapatılan katman çıkar; aynı tablo tekrar yazılmaz (gereksiz geri alma noktası yok).
        rig.Pipeline.SetLayer(ColorLayer.Dimming, null);
        Assert.True(rig.Pipeline.Apply());
        Assert.True(rig.Pipeline.Apply());
        Assert.Equal(Combiner.Combine([Calibration, Warm]), rig.Hw.Gpu.Read(Display));
        Assert.Equal(2, rig.Hw.Gpu.Writes.Count);
    }

    /// <summary>Tablo zaten ekrandaysa (açılışta birim tablo) yazılmaz.</summary>
    [Fact]
    public void Apply_WithoutLayers_DoesNotRewriteIdentityTable()
    {
        var rig = new Rig();
        Assert.True(rig.Pipeline.Apply());
        Assert.Empty(rig.Hw.Gpu.Writes);
    }

    /// <summary>Boru hattının yazması Güvenlik'ten geri alınır; geri yüklenen tablo kullanıcının tablosu olur.</summary>
    [Fact]
    public void Apply_IsRolledBackThroughSafety_AndRestoredTableIsNotExternalChange()
    {
        var rig = new Rig();
        rig.Pipeline.SetLayer(ColorLayer.Dimming, Dim);
        Assert.True(rig.Pipeline.Apply());

        rig.Safety.RollBack(rig.Hw.Gpu.Writes[0].RestorePoint);

        Assert.Equal(GammaRamp.Identity, rig.Hw.Gpu.Read(Display));
        Assert.False(rig.Pipeline.RestoreIfChangedExternally());
        Assert.Throws<ArgumentException>(() => rig.Pipeline.Restore(new DisplayId(@"\\.\DISPLAY2"), Dim));
    }

    /// <summary>Dış program tabloyu değiştirince kullanıcının tablosu geri yüklenir ve uyarı verilir.</summary>
    [Fact]
    public void ExternalChange_IsDetected_AndUserTableRestored()
    {
        var rig = new Rig();
        Assert.False(rig.Pipeline.RestoreIfChangedExternally()); // henüz tablo yazılmadı: bilinen tablo yok, dokunulmaz

        rig.Pipeline.SetLayer(ColorLayer.Calibration, Calibration);
        rig.Pipeline.SetLayer(ColorLayer.BlueLight, Warm);
        Assert.True(rig.Pipeline.Apply());
        var user = rig.Hw.Gpu.Read(Display);
        Assert.False(rig.Pipeline.RestoreIfChangedExternally());

        rig.Hw.Gpu.SimulateExternalChange(Display, NightLight); // Windows Gece ışığı
        Assert.True(rig.Pipeline.RestoreIfChangedExternally());
        Assert.Equal(user, rig.Hw.Gpu.Read(Display));
        Assert.False(rig.Pipeline.RestoreIfChangedExternally());
        Assert.Equal(2, rig.Hw.Gpu.Writes.Count);

        // Okunamazsa karar verilmez; geri yükleme reddedilse de uyarı verilir.
        rig.Hw.Gpu.SimulateExternalChange(Display, NightLight);
        rig.Hw.Gpu.ReadFails = true;
        Assert.False(rig.Pipeline.RestoreIfChangedExternally());
        rig.Hw.Gpu.ReadFails = false;
        rig.Hw.Gpu.RejectWrites = true;
        Assert.True(rig.Pipeline.RestoreIfChangedExternally());
        Assert.Equal(NightLight, rig.Hw.Gpu.Read(Display));
    }

    /// <summary>Sürücü tabloyu yuvarlayarak saklasa da kendi yazması dış değişiklik sayılmaz (sonsuz geri yükleme olmaz).</summary>
    [Fact]
    public void DriverRounding_IsNotMistakenForExternalChange()
    {
        var rig = new Rig();
        rig.Hw.Gpu.StoredBits = 10;
        rig.Pipeline.SetLayer(ColorLayer.BlueLight, Warm);

        Assert.True(rig.Pipeline.Apply());
        Assert.NotEqual(Warm, rig.Hw.Gpu.Read(Display));

        Assert.False(rig.Pipeline.RestoreIfChangedExternally());
        Assert.True(rig.Pipeline.Apply());
        Assert.Single(rig.Hw.Gpu.Writes);
    }

    /// <summary>Reddedilen ya da okunamayan tabloda Apply false döner; kullanıcının tablosu ekrandakinde kalır.</summary>
    [Fact]
    public void Apply_ReportsFailure_AndKeepsLastUserTable()
    {
        var rig = new Rig();
        rig.Pipeline.SetLayer(ColorLayer.Dimming, Dim);
        Assert.True(rig.Pipeline.Apply());

        rig.Hw.Gpu.RejectWrites = true; // ör. GdiIcmGammaRange yokken derin karartma
        rig.Pipeline.SetLayer(ColorLayer.Dimming, GammaRamps.Scale(new RgbGains(0.15, 0.15, 0.15)));
        Assert.False(rig.Pipeline.Apply());
        Assert.Equal(Dim, rig.Hw.Gpu.Read(Display));
        Assert.False(rig.Pipeline.RestoreIfChangedExternally());

        rig.Hw.Gpu.RejectWrites = false;
        rig.Hw.Gpu.ReadFails = true; // yedeksiz yazılmaz
        Assert.False(rig.Pipeline.Apply());
        Assert.Single(rig.Hw.Gpu.Writes);
    }

    /// <summary>Kapanışta tabloda sadece kalibrasyon kalır.</summary>
    [Fact]
    public void Shutdown_LeavesOnlyCalibration()
    {
        var rig = new Rig();
        rig.Pipeline.SetLayer(ColorLayer.Calibration, Calibration);
        rig.Pipeline.SetLayer(ColorLayer.BlueLight, Warm);
        rig.Pipeline.SetLayer(ColorLayer.Dimming, Dim);
        Assert.True(rig.Pipeline.Apply());

        rig.Pipeline.Shutdown();
        Assert.Equal(Calibration, rig.Hw.Gpu.Read(Display));
        Assert.True(rig.Pipeline.Apply()); // efekt katmanları kalktı
        Assert.Equal(Calibration, rig.Hw.Gpu.Read(Display));

        var noCalibration = new Rig();
        noCalibration.Pipeline.SetLayer(ColorLayer.BlueLight, Warm);
        Assert.True(noCalibration.Pipeline.Apply());
        noCalibration.Pipeline.Shutdown();
        Assert.Equal(GammaRamp.Identity, noCalibration.Hw.Gpu.Read(Display));
    }

    /// <summary>Kurtarma ve kaldırma: birim tablo, kalibrasyon dahil hiçbir katman kalmaz.</summary>
    [Fact]
    public void ResetTable_WritesIdentity_AndClearsLayers()
    {
        var rig = new Rig();
        rig.Pipeline.SetLayer(ColorLayer.Calibration, Calibration);
        rig.Pipeline.SetLayer(ColorLayer.BlueLight, Warm);
        Assert.True(rig.Pipeline.Apply());

        rig.Pipeline.ResetTable();
        Assert.Equal(GammaRamp.Identity, rig.Hw.Gpu.Read(Display));
        Assert.False(rig.Pipeline.RestoreIfChangedExternally());
        Assert.True(rig.Pipeline.Apply());
        Assert.Equal(2, rig.Hw.Gpu.Writes.Count);
    }

    /// <summary>GdiIcmGammaRange yazılmadan önce eski değer yedeklenir.</summary>
    [Fact]
    public void EnsureFullGammaRange_BacksUpOldRegistryValue()
    {
        var rig = new Rig();
        Assert.True(rig.Hw.GammaRange.Write(128, FakeRestorePoints.For(WriteTarget.GdiIcmGammaRange, Display))); // kullanıcının eski değeri

        Assert.True(rig.Pipeline.EnsureFullGammaRange());
        Assert.Equal(ColorPipeline.FullGammaRange, rig.Hw.GammaRange.Read());
        Assert.Equal(2, rig.Hw.GammaRange.WriteCount);
        Assert.True(rig.Pipeline.EnsureFullGammaRange()); // yönetici onayı bir kez: değer zaten 256
        Assert.Equal(2, rig.Hw.GammaRange.WriteCount);

        rig.Safety.CleanUpForUninstall();
        Assert.Equal(128, rig.Hw.GammaRange.Read());
    }

    /// <summary>Kullanıcı yönetici onayını vermezse değer değişmez; sonra tekrar sorulabilir.</summary>
    [Fact]
    public void EnsureFullGammaRange_WhenAdminDeclines_ReturnsFalse()
    {
        var rig = new Rig();
        rig.Hw.GammaRange.AdminApproves = false;
        Assert.False(rig.Pipeline.EnsureFullGammaRange());
        Assert.Null(rig.Hw.GammaRange.Read());

        rig.Hw.GammaRange.AdminApproves = true;
        Assert.True(rig.Pipeline.EnsureFullGammaRange());
        rig.Safety.CleanUpForUninstall();
        Assert.Null(rig.Hw.GammaRange.Read()); // eski hâli "değer yok"
    }

    private static GammaRamp Curve(double red, double green, double blue)
    {
        return new GammaRamp(Channel(red), Channel(green), Channel(blue));
        static ushort[] Channel(double exponent) =>
            [.. Enumerable.Range(0, GammaRamp.Size).Select(i => (ushort)Math.Round(65535 * Math.Pow(i / 255.0, exponent)))];
    }

    /// <summary>Sahte sistem + gerçek Güvenlik katmanı + boru hattı (Güvenlik GPU tablosunu boru hattından geri yükler).</summary>
    private sealed class Rig
    {
        public Rig()
        {
            Safety = new SafetyLayer(
                Display, [], new SafetyHardware(Hw.Monitor, Hw.Gpu, Hw.Modes, Hw.Nvidia, Hw.GammaRange),
                () => Pipeline!, new MemoryBackupStore(), Hw.Clock);
            Pipeline = new ColorPipeline(Display, Hw.Gpu, Hw.GammaRange, Combiner, Safety);
        }

        public FakeHardware Hw { get; } = new();
        public SafetyLayer Safety { get; }
        public ColorPipeline Pipeline { get; }
    }

    private sealed class MemoryBackupStore : IFactoryBackupStore
    {
        private FactoryBackup? saved;
        public FactoryBackup? Load() => saved;
        public void Save(FactoryBackup backup) => saved = backup;
    }
}
