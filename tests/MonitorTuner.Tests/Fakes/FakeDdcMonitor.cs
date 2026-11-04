using MonitorTuner.Core;
using MonitorTuner.Hardware.Abstractions;

namespace MonitorTuner.Tests.Fakes;

internal sealed record DdcWrite(byte Code, ushort Value, RestorePoint RestorePoint);

/// <summary>
/// Sahte monitör (DDC/CI). DDC'li hâli kullanıcının ünitesinden kaydedilen kodlarla başlar
/// (<c>donanim/hp-p21b-g4/ddc-vcp-60hz.txt</c>). Yasak kodlara (04/05/08, E6–FF) yazmayı gerçek monitör gibi
/// kabul eder: engellemek üst katmanın işi, testler <see cref="WriteCount"/> ile yakalar.
/// </summary>
internal sealed class FakeDdcMonitor : IDdcMonitor
{
    private sealed record VcpCode(byte Code, ushort? Initial, ushort Maximum, bool Readable, bool Writable, ushort[] Possible);

    private static readonly Lazy<IReadOnlyList<VcpCode>> Recorded = new(() => Parse(
        File.ReadAllText(Path.Combine(RepoRoot.Find(), "donanim", "hp-p21b-g4", "ddc-vcp-60hz.txt"))));

    private readonly DisplayId display;
    private readonly Dictionary<byte, VcpCode>? codes;
    private readonly Dictionary<byte, ushort> current = [];
    private readonly List<DdcWrite> writes = [];
    private readonly string? capabilities;

    private FakeDdcMonitor(DisplayId display, IReadOnlyList<VcpCode>? codes)
    {
        this.display = display;
        if (codes is null) return;
        this.codes = codes.ToDictionary(c => c.Code);
        foreach (var c in codes)
            if (c.Initial is { } v) current[c.Code] = v;
        // Kaydedilmiş yetenek dizesi yok; kodlardan MCCS biçiminde üretilir.
        var vcp = codes.Select(c => c.Possible.Length == 0
            ? $"{c.Code:X2}"
            : $"{c.Code:X2}({string.Join(" ", c.Possible.Select(p => $"{p:X2}"))})");
        capabilities = $"(prot(monitor)type(lcd)model(HP P21b G4)cmds(01 02 03 07 0C F3)vcp({string.Join(" ", vcp)})mccs_ver(2.2))";
    }

    public static FakeDdcMonitor HpP21bG4(DisplayId? display = null) => new(display ?? FakeHardware.Display, Recorded.Value);

    public static FakeDdcMonitor WithoutDdc(DisplayId? display = null) => new(display ?? FakeHardware.Display, null);

    /// <summary>false: monitör DDC'ye cevap vermiyor (kablo, uyku); okuma null, yazma false.</summary>
    public bool Responding { get; set; } = true;

    /// <summary>Monitörün belleğine ulaşan yazmalar, sırayla.</summary>
    public IReadOnlyList<DdcWrite> Writes => writes;

    public int WriteCount(byte code) => writes.Count(w => w.Code == code);

    public string? ReadCapabilities(DisplayId display) => Reachable(display) ? capabilities : null;

    public DdcValue? Read(DisplayId display, byte code) =>
        Reachable(display) && codes!.TryGetValue(code, out var vcp) && vcp.Readable && current.TryGetValue(code, out var value)
            ? new DdcValue(value, vcp.Maximum)
            : null;

    public bool Write(DisplayId display, byte code, ushort value, RestorePoint restorePoint)
    {
        FakeRestorePoints.Check(restorePoint, WriteTarget.Ddc, display, code);
        if (!Reachable(display) || !codes!.TryGetValue(code, out var vcp) || !vcp.Writable) return false;
        var outOfRange = vcp.Possible.Length > 0 ? !vcp.Possible.Contains(value) : vcp.Maximum > 0 && value > vcp.Maximum;
        if (outOfRange) return false;
        current[code] = value;
        writes.Add(new DdcWrite(code, value, restorePoint));
        return true;
    }

    /// <summary>Monitörün kendi değiştirdiği değer (OSD tuşu, mod değişince AE); yazma sayılmaz. DDC yoksa etkisiz.</summary>
    public void SetMonitorValue(byte code, ushort value)
    {
        if (codes is null) return;
        if (!codes.ContainsKey(code)) throw new ArgumentException($"Monitörde {code:X2} kodu yok.", nameof(code));
        current[code] = value;
    }

    private bool Reachable(DisplayId display) => codes is not null && Responding && display == this.display;

    private static IReadOnlyList<VcpCode> Parse(string text)
    {
        var result = new Dictionary<byte, VcpCode>(); // dosyada D6 iki kez var
        foreach (var block in text.Split("=================================================="))
        {
            var fields = block.Split('\n').Select(l => l.Split(':', 2)).Where(p => p.Length == 2)
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
            if (!fields.TryGetValue("VCP Code", out var hex)) continue;
            var code = Convert.ToByte(hex, 16);
            var access = fields["Read-Write"]; // boş: üreticiye özel, erişim yazılmamış
            ushort? initial = fields["Current Value"] is { Length: > 0 } c ? ushort.Parse(c) : null;
            var max = fields["Maximum Value"] is { Length: > 0 } m ? ushort.Parse(m) : (ushort)0;
            var possible = fields["Possible Values"].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ushort.Parse).ToArray();
            result[code] = new VcpCode(code, initial, max, access != "Write Only" && initial is not null, access != "Read Only", possible);
        }
        return result.Values.OrderBy(c => c.Code).ToList();
    }
}
