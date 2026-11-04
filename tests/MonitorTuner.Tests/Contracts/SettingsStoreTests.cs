using MonitorTuner.Core;
using MonitorTuner.Services;
using MonitorTuner.Tests.Fakes;

namespace MonitorTuner.Tests.Contracts;

/// <summary>Ayar deposu sözleşmesi (02-MIMARI.md), geçici klasör ve sahte saatle (T102).</summary>
public sealed class SettingsStoreTests : IDisposable
{
    private sealed record Sample(int Brightness, string Name);

    private static readonly MonitorKey Hp = new("HP P21b G4", "1CR1481CN2", "HDMI", "NVIDIA GeForce RTX 3080");

    // Deponun klasörü, deney klasörünün içindeki "AppData" altında; dışına taşan dosya "AppData"nın kardeşlerinde görünür.
    private readonly string _sandbox = Path.Combine(Path.GetTempPath(), "mt-settings-" + Guid.NewGuid().ToString("N"));
    private readonly FakeHardware _hardware = new();

    private string Root => Path.Combine(_sandbox, "AppData", "MonitorTuner");

    private JsonSettingsStore NewStore() => new(Root, _hardware.Clock);

    public SettingsStoreTests() => Directory.CreateDirectory(Path.Combine(_sandbox, "AppData"));

    public void Dispose()
    {
        if (Directory.Exists(_sandbox)) Directory.Delete(_sandbox, recursive: true);
    }

    private string[] AllFiles() => Directory.GetFiles(_sandbox, "*", SearchOption.AllDirectories);

    /// <summary>Bölüm yazılıp okunur; anahtar monitör + seri no + port + kart.</summary>
    [Fact]
    public void WriteThenRead_RoundTripsSection_PerMonitorKey()
    {
        var store = NewStore();
        store.Write(Hp, "RefreshRates", new Sample(70, "76 Hz"));
        store.Write(Hp with { SerialNumber = "BASKA" }, "RefreshRates", new Sample(1, "başka seri"));
        store.Write(Hp with { Port = "DP" }, "RefreshRates", new Sample(2, "başka port"));
        store.Write(Hp with { AdapterName = "NVIDIA GeForce RTX 3090" }, "RefreshRates", new Sample(3, "başka kart"));

        Assert.Equal(new Sample(70, "76 Hz"), store.Read<Sample>(Hp, "RefreshRates"));
        Assert.Equal(new Sample(1, "başka seri"), store.Read<Sample>(Hp with { SerialNumber = "BASKA" }, "RefreshRates"));
        Assert.Equal(new Sample(2, "başka port"), store.Read<Sample>(Hp with { Port = "DP" }, "RefreshRates"));
        Assert.Equal(new Sample(3, "başka kart"), store.Read<Sample>(Hp with { AdapterName = "NVIDIA GeForce RTX 3090" }, "RefreshRates"));
    }

    /// <summary>Geçici klasör dışında dosya oluşmaz; yedekler ayrı klasörde.</summary>
    [Fact]
    public void Write_TouchesOnlyFilesInsideItsFolder()
    {
        var store = NewStore();
        var before = Directory.GetFileSystemEntries(Path.Combine(_sandbox, "AppData"));

        store.Write(Hp, "ColorProfiles", new Sample(1, "x"));
        store.Write(new MonitorKey(@"..\..\Kötü: *?", "\0", "a/b", @"C:\Windows"), "BlueLight", new Sample(2, "y"));

        Assert.Equal([Root], before); // klasör depo kurulunca var; yazma yanına bir şey eklemez
        Assert.Equal(before, Directory.GetFileSystemEntries(Path.Combine(_sandbox, "AppData")));
        Assert.All(AllFiles(), f => Assert.StartsWith(Root, f));
        Assert.Equal(2, Directory.GetFiles(Root, "*.json", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public void BackupFolder_IsSeparateFromSettings_AndExists()
    {
        var store = NewStore();
        store.Write(Hp, "ColorProfiles", new Sample(1, "x"));

        Assert.True(Directory.Exists(store.BackupFolder));
        Assert.StartsWith(Root, store.BackupFolder);
        var settingsFolder = Path.GetDirectoryName(Assert.Single(Directory.GetFiles(Root, "*.json", SearchOption.AllDirectories)))!;
        var sep = Path.DirectorySeparatorChar;
        Assert.NotEqual(settingsFolder, store.BackupFolder);
        Assert.False((store.BackupFolder + sep).StartsWith(settingsFolder + sep), "yedek klasörü ayar klasörünün içinde");
        Assert.False((settingsFolder + sep).StartsWith(store.BackupFolder + sep), "ayar klasörü yedek klasörünün içinde");
    }

    /// <summary>Her özellik kendi bölümünü yazar; biri diğerini ezmez ve yeni bir depo aynı dosyayı okur (yeniden başlatma).</summary>
    [Fact]
    public void Write_KeepsOtherSections_AndSurvivesRestart()
    {
        NewStore().Write(Hp, "RefreshRates", new Sample(1, "a"));
        NewStore().Write(Hp, "ColorProfiles", new Sample(2, "b"));
        NewStore().Write(Hp, "RefreshRates", new Sample(3, "c")); // aynı bölümün üstüne yazma

        var store = NewStore();
        Assert.Equal(new Sample(3, "c"), store.Read<Sample>(Hp, "RefreshRates"));
        Assert.Equal(new Sample(2, "b"), store.Read<Sample>(Hp, "ColorProfiles"));
    }

    [Fact]
    public void Read_MissingMonitorOrSection_ReturnsDefault()
    {
        var store = NewStore();
        Assert.Null(store.Read<Sample>(Hp, "RefreshRates"));
        store.Write(Hp, "ColorProfiles", new Sample(1, "x"));
        Assert.Null(store.Read<Sample>(Hp, "RefreshRates"));
        Assert.Equal(0, store.Read<int>(Hp, "RefreshRates")); // değer türünde varsayılan
    }

    /// <summary>Bölüm başka bir türde okunursa varsayılan döner; dosya ve bölüm bozulmaz.</summary>
    [Fact]
    public void Read_SectionOfWrongShape_ReturnsDefaultWithoutThrowing()
    {
        var store = NewStore();
        store.Write(Hp, "RefreshRates", "metin");

        Assert.Null(store.Read<Sample>(Hp, "RefreshRates"));
        Assert.Equal("metin", store.Read<string>(Hp, "RefreshRates"));
    }

    /// <summary>Bozuk dosya sessizce ezilmez: okuma varsayılan döner; yazma önce bozuk dosyayı yedek klasörüne kopyalar.</summary>
    [Fact]
    public void Write_CorruptFile_IsCopiedToBackupFolder_BeforeStartingFresh()
    {
        var store = NewStore();
        store.Write(Hp, "RefreshRates", new Sample(1, "a"));
        var file = Assert.Single(Directory.GetFiles(Root, "*.json", SearchOption.AllDirectories));
        File.WriteAllText(file, "{ bozuk json");

        Assert.Null(store.Read<Sample>(Hp, "RefreshRates"));
        Assert.Equal("{ bozuk json", File.ReadAllText(file)); // okuma dokunmaz

        _hardware.Clock.Advance(TimeSpan.FromMinutes(5));
        store.Write(Hp, "ColorProfiles", new Sample(2, "b"));

        var copy = Assert.Single(Directory.GetFiles(store.BackupFolder));
        Assert.Equal("{ bozuk json", File.ReadAllText(copy));
        Assert.Contains("bozuk", Path.GetFileName(copy));
        Assert.Equal(new Sample(2, "b"), store.Read<Sample>(Hp, "ColorProfiles"));
    }

    /// <summary>Yazma geçici dosya bırakmaz; ayar dosyası her an ya eski ya yeni tam hâlindedir.</summary>
    [Fact]
    public void Write_LeavesNoTemporaryFiles()
    {
        var store = NewStore();
        for (var i = 0; i < 5; i++) store.Write(Hp, "RefreshRates", new Sample(i, "n"));

        var file = Assert.Single(AllFiles());
        Assert.EndsWith(".json", file);
    }

    /// <summary>Farklı bölümlere aynı anda yazan iki özellik birbirinin yazısını kaybettirmez.</summary>
    [Fact]
    public void ParallelWrites_ToDifferentSections_AreAllKept()
    {
        var store = NewStore();

        Parallel.For(0, 30, i => store.Write(Hp, "Bolum" + i, new Sample(i, "p")));

        for (var i = 0; i < 30; i++) Assert.Equal(new Sample(i, "p"), store.Read<Sample>(Hp, "Bolum" + i));
    }

    [Fact]
    public void Write_RejectsEmptySectionAndNullValue()
    {
        var store = NewStore();
        Assert.Throws<ArgumentException>(() => store.Write(Hp, "", new Sample(1, "x")));
        Assert.Throws<ArgumentException>(() => store.Read<Sample>(Hp, " "));
        Assert.Throws<ArgumentNullException>(() => store.Write<Sample>(Hp, "RefreshRates", null!));
    }

    [Fact]
    public void DefaultRoot_IsUnderAppData()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        Assert.StartsWith(appData, JsonSettingsStore.DefaultRoot());
        Assert.EndsWith("MonitorTuner", JsonSettingsStore.DefaultRoot());
    }
}
