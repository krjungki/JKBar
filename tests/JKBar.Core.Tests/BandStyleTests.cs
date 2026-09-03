// Guards the premultiplication, where getting it wrong shows up as a washed-out band rather than an exception.
using System.Drawing;
using JKBar.Core.Layout;

namespace JKBar.Core.Tests;

public class BandStyleTests
{
    [Fact]
    public void LeavesAnOpaqueColourAlone()
    {
        var resolved = new BandStyle(Color.FromArgb(40, 80, 160), 100).ForLayeredSurface();

        Assert.Equal(255, resolved.A);
        Assert.Equal(40, resolved.R);
        Assert.Equal(80, resolved.G);
        Assert.Equal(160, resolved.B);
    }

    /// <summary>Half transparent means half the colour too, or the surface reads brighter than the choice made.</summary>
    [Fact]
    public void ScalesTheColourWithTheAlpha()
    {
        var resolved = new BandStyle(Color.FromArgb(200, 100, 50), 50).ForLayeredSurface();

        Assert.Equal(128, resolved.A);
        Assert.Equal(100, resolved.R);
        Assert.Equal(50, resolved.G);
        Assert.Equal(25, resolved.B);
    }

    [Fact]
    public void DisappearsEntirelyAtZero()
    {
        var resolved = new BandStyle(Color.White, 0).ForLayeredSurface();

        Assert.Equal(0, resolved.A);
        Assert.Equal(0, resolved.R);
        Assert.Equal(0, resolved.G);
        Assert.Equal(0, resolved.B);
    }

    [Theory]
    [InlineData(-40, 0)]
    [InlineData(140, 100)]
    public void ClampsOpacityToARange(int given, int expected)
    {
        Assert.Equal(expected, new BandStyle(Color.Black, given).Opacity);
    }

    [Fact]
    public void DefaultsToTheChosenBandStyle()
    {
        Assert.Equal(Color.White, BandStyle.Default.Colour);
        Assert.Equal(25, BandStyle.Default.Opacity);
    }
}
