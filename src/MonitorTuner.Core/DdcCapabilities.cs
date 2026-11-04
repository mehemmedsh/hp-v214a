namespace MonitorTuner.Core;

/// <summary>DDC/CI yetenek dizesi (MCCS): monitörün desteklediği VCP kodları. Saf mantık, donanımsız test edilir.</summary>
public static class DdcCapabilities
{
    /// <summary>
    /// <c>vcp(...)</c> listesindeki kodlar; parantez içindeki olası değerler atlanır. Kodlar boşluklu
    /// (<c>vcp(10 12)</c>) ya da bitişik (<c>vcp(1012)</c>) olabilir. null: liste yok ya da kesik.
    /// </summary>
    public static IReadOnlySet<byte>? ParseVcpCodes(string capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        var start = capabilities.IndexOf("vcp(", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;

        var codes = new HashSet<byte>();
        var depth = 1;
        int? high = null; // iki haneli kodun ilk hanesi
        for (var i = start + 4; i < capabilities.Length; i++)
        {
            var ch = capabilities[i];
            if (ch == '(') depth++;
            else if (ch == ')' && --depth == 0) return codes;

            if (depth != 1 || !Uri.IsHexDigit(ch))
            {
                high = null;
                continue;
            }
            if (high is { } h)
            {
                codes.Add((byte)(h * 16 + Uri.FromHex(ch)));
                high = null;
            }
            else high = Uri.FromHex(ch);
        }
        return null;
    }
}
