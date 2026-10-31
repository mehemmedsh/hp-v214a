using MonitorTuner.Core;

namespace MonitorTuner.Tests.Contracts;

/// <summary>03-GUVENLIK.md'deki sayılar ve GPU tablosu biçimi.</summary>
public class CoreContractTests
{
    [Fact]
    public void SafetyLimits_MatchSecurityDoc()
    {
        Assert.Equal(TimeSpan.FromSeconds(15), SafetyLimits.ConfirmationTimeout);
        Assert.Equal(0.15, SafetyLimits.MinWhiteLevel);
        Assert.Equal(60, SafetyLimits.OriginalRefreshHz);
    }

    [Theory]
    [InlineData(0x04, true)]
    [InlineData(0x05, true)]
    [InlineData(0x08, true)]
    [InlineData(0xE6, true)]
    [InlineData(0xFF, true)]
    [InlineData(0x10, false)]
    [InlineData(0x14, false)]
    [InlineData(0x16, false)]
    [InlineData(0xE5, false)]
    public void DdcCodes_04_05_08_AndE6ToFF_AreNeverWritten(byte code, bool forbidden) =>
        Assert.Equal(forbidden, SafetyLimits.IsDdcWriteForbidden(code));

    [Fact]
    public void GammaRamp_Identity_IsLinearFullRange()
    {
        var ramp = GammaRamp.Identity;
        Assert.Equal(GammaRamp.Size, ramp.Red.Count);
        Assert.Equal((ushort)0, ramp.Green[0]);
        Assert.Equal((ushort)(128 * 257), ramp.Blue[128]);
        Assert.Equal(ushort.MaxValue, ramp.Red[255]);
    }

    [Fact]
    public void GammaRamp_RejectsWrongSize_AndComparesByValue()
    {
        var full = new ushort[GammaRamp.Size];
        Assert.Throws<ArgumentException>(() => new GammaRamp(new ushort[255], full, full));

        var copy = new GammaRamp(GammaRamp.Identity.Red.ToArray(), GammaRamp.Identity.Green.ToArray(), GammaRamp.Identity.Blue.ToArray());
        Assert.Equal(GammaRamp.Identity, copy);
        Assert.Equal(GammaRamp.Identity.GetHashCode(), copy.GetHashCode());
        Assert.NotEqual(GammaRamp.Identity, new GammaRamp(full, full, full));
    }

    [Fact]
    public void GammaRamp_CopiesInput()
    {
        var red = GammaRamp.Identity.Red.ToArray();
        var ramp = new GammaRamp(red, red, red);
        red[10] = 0;
        Assert.Equal(GammaRamp.Identity, ramp);
    }
}
