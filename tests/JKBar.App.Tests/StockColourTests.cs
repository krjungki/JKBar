// Checks that the quote's rise and fall colours stay readable whichever way the band's text colour went.
using JKBar.App.Rendering;
using JKBar.Core.Layout;
using JKBar.Core.Stocks;

namespace JKBar.App.Tests;

public sealed class StockColourTests
{
    [Theory]
    [InlineData(StockDirection.Rising)]
    [InlineData(StockDirection.Falling)]
    public void MoveColourKeepsAaContrastOnTheBandBehindTheText(StockDirection direction)
    {
        // Light text sits on a dark band and dark text on a light one; each pairing is checked against its own band.
        var darkBand = ColourContrast.RelativeLuminance(Color.FromArgb(40, 44, 52));
        var onDark = BandRenderer.StockColour(direction, AdaptiveAppearance.LightText);

        Assert.True(ColourContrast.Ratio(ColourContrast.RelativeLuminance(onDark), darkBand) >= 4.5);
        Assert.NotEqual(onDark, BandRenderer.StockColour(direction, AdaptiveAppearance.DarkText));
    }

    [Fact]
    public void UnchangedQuoteUsesTheTextColour()
    {
        Assert.Equal(AdaptiveAppearance.DarkText, BandRenderer.StockColour(StockDirection.Flat, AdaptiveAppearance.DarkText));
    }

    [Fact]
    public void DownloadMarkerKeepsAaContrastOnADarkBand()
    {
        var darkBand = ColourContrast.RelativeLuminance(Color.FromArgb(40, 44, 52));
        var onDark = BandRenderer.NetworkDownColour(AdaptiveAppearance.LightText);

        Assert.True(ColourContrast.Ratio(ColourContrast.RelativeLuminance(onDark), darkBand) >= 4.5);
        Assert.Equal(Color.FromArgb(38, 103, 196), BandRenderer.NetworkDownColour(AdaptiveAppearance.DarkText));
    }

    [Fact]
    public void DiskReadMarkerKeepsAaContrastOnADarkBand()
    {
        var darkBand = ColourContrast.RelativeLuminance(Color.FromArgb(40, 44, 52));
        var onDark = BandRenderer.DiskReadColour(AdaptiveAppearance.LightText);

        Assert.True(ColourContrast.Ratio(ColourContrast.RelativeLuminance(onDark), darkBand) >= 4.5);
        Assert.Equal(Color.FromArgb(32, 145, 88), BandRenderer.DiskReadColour(AdaptiveAppearance.DarkText));
    }
}
