namespace MonitorTuner.Core;

/// <summary>GPU renk tablosu (Windows gamma tablosu biçimi): kanal başına 256 giriş, 0–65535. Değiştirilemez.</summary>
public sealed class GammaRamp : IEquatable<GammaRamp>
{
    public const int Size = 256;

    public GammaRamp(ushort[] red, ushort[] green, ushort[] blue)
    {
        Red = Copy(red, nameof(red));
        Green = Copy(green, nameof(green));
        Blue = Copy(blue, nameof(blue));
    }

    /// <summary>Birim tablo: tablo yokmuş gibi (giriş = çıkış).</summary>
    public static GammaRamp Identity { get; } = CreateIdentity();

    public IReadOnlyList<ushort> Red { get; }
    public IReadOnlyList<ushort> Green { get; }
    public IReadOnlyList<ushort> Blue { get; }

    public bool Equals(GammaRamp? other) =>
        other is not null && Red.SequenceEqual(other.Red) && Green.SequenceEqual(other.Green) && Blue.SequenceEqual(other.Blue);

    public override bool Equals(object? obj) => Equals(obj as GammaRamp);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var channel in new[] { Red, Green, Blue })
            foreach (var value in channel) hash.Add(value);
        return hash.ToHashCode();
    }

    private static IReadOnlyList<ushort> Copy(ushort[] values, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        if (values.Length != Size) throw new ArgumentException($"Kanal {Size} giriş olmalı, {values.Length} geldi.", name);
        return Array.AsReadOnly((ushort[])values.Clone());
    }

    private static GammaRamp CreateIdentity()
    {
        var linear = Enumerable.Range(0, Size).Select(i => (ushort)(i * 257)).ToArray();
        return new GammaRamp(linear, linear, linear);
    }
}
