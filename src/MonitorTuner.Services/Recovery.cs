using MonitorTuner.Color;
using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;
using MonitorTuner.Safety;

namespace MonitorTuner.Services;

/// <summary>
/// Kurtarma (T104): bir ekranda GPU tablosunu sıfırlar (birim tablo, renk boru hattı üzerinden), dithering ve canlılığı
/// sürücü varsayılanına alır, <c>GdiIcmGammaRange</c>'i ilk açılış yedeğindeki değere döndürür. Uygulamanın ayarlarına,
/// DDC'ye ve Hz'e dokunmaz. Her yazma Güvenlik'ten geri alma noktası alır; zaten varsayılan olan yazılmaz. Bir adım
/// başarısız olsa da (güvenli modda NVIDIA sürücüsü yok, UAC'de "Hayır") diğerleri yapılır.
/// </summary>
public sealed class Recovery(
    DisplayId display,
    IColorPipeline pipeline,
    IGpuGammaTable gpuTable,
    IGdiIcmGammaRange gammaRange,
    INvidiaSettings nvidia,
    ISafetyLayer safety,
    IFactoryBackupStore factory) : IRecovery
{
    /// <summary>Sürücü varsayılanı: dithering sürücüye bırakılmış, Digital Vibrance %50.</summary>
    public static readonly NvidiaState DriverDefaults = new(DitheringMode.DriverDefault, 50);

    /// <summary>
    /// Kurtarma dosyasının bağlaması: kendi Güvenlik katmanı ve renk boru hattı. İlk açılış yedeği sadece okunur;
    /// yedek yoksa kurtarma yenisini oluşturmaz (o anki bozuk hâl "fabrika ayarı" olarak kaydedilmesin).
    /// </summary>
    public static Recovery Create(DisplayId display, SafetyHardware hardware, IFactoryBackupStore factory, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(hardware);
        var store = new ReadOnlyBackupStore(factory ?? throw new ArgumentNullException(nameof(factory)));
        ColorPipeline? pipeline = null;
        var safety = new SafetyLayer(display, [], hardware, () => pipeline!, store, time);
        pipeline = new ColorPipeline(display, hardware.GpuTable, hardware.GammaRange, new GammaTableCombiner(), safety);
        return new Recovery(display, pipeline, hardware.GpuTable, hardware.GammaRange, hardware.Nvidia, safety, store);
    }

    public RecoveryReport ResetToDefaults()
    {
        var table = Attempt(ResetTable);
        Attempt(ResetNvidia);
        var now = Attempt(() => nvidia.Read(display));
        var range = Attempt(RestoreGammaRange); // tablo sıfırlandıktan sonra (kaldırmadaki sıra)
        return new RecoveryReport(table, range, now?.Dithering == DriverDefaults.Dithering, now?.Vibrance == DriverDefaults.Vibrance);
    }

    private bool ResetTable()
    {
        pipeline.ResetTable();
        return gpuTable.Read(display) is { } now && ColorPipeline.IsNeutral(now);
    }

    private bool ResetNvidia()
    {
        if (nvidia.Read(display) is not { } now) return false; // güvenli mod ya da ekran NVIDIA kartında değil
        if (now == DriverDefaults) return true;
        var point = safety.CreateRestorePoint(new WriteRequest(WriteTarget.NvidiaSettings, display));
        return nvidia.ResetToDefaults(display, point);
    }

    /// <summary>Yedek yoksa uygulama bu değere hiç yazmamıştır (her yazmadan önce yedek alınır): dokunulmaz.</summary>
    private bool RestoreGammaRange()
    {
        if (factory.Load() is not { } backup) return true;
        if (gammaRange.Read() == backup.GammaRange) return true; // yönetici onayı istenmez
        var point = safety.CreateRestorePoint(new WriteRequest(WriteTarget.GdiIcmGammaRange, display));
        return gammaRange.Write(backup.GammaRange, point);
    }

    // Kurtarma yarıda kalmamalı: sürücü, kayıt defteri ya da Güvenlik hatası o adımı başarısız sayar.
    private static T? Attempt<T>(Func<T> step)
    {
        try
        {
            return step();
        }
        catch (Exception)
        {
            return default;
        }
    }

    /// <summary>İlk açılış yedeğini okur, hiç yazmaz; okunamazsa yedek yok sayılır.</summary>
    private sealed class ReadOnlyBackupStore(IFactoryBackupStore inner) : IFactoryBackupStore
    {
        public FactoryBackup? Load()
        {
            try
            {
                return inner.Load();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return null; // yedek okunamıyor diye tablo ve NVIDIA adımları da düşmesin
            }
        }

        public void Save(FactoryBackup backup)
        {
        }
    }
}
