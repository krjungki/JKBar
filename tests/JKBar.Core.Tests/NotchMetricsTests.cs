// Guards the measured macOS figures and the DPI scaling, where a rounding slip would show as a lopsided corner.
using JKBar.Core.Layout;

namespace JKBar.Core.Tests;

public class NotchMetricsTests
{
    /// <summary>
    /// Measured through NSScreen on a 14-inch MacBook Pro: 185 x 32 pt, bottom corner radius near 8.
    /// If these change it should be a decision, not a drift.
    /// </summary>
    [Fact]
    public void KeepsTheMeasuredMacBookPro14Figures()
    {
        Assert.Equal(185, NotchMetrics.MacBookPro14.Width);
        Assert.Equal(32, NotchMetrics.MacBookPro14.Height);
        Assert.Equal(8, NotchMetrics.MacBookPro14.BottomCornerRadius);
    }

    [Fact]
    public void ScalesEveryDimension()
    {
        var scaled = NotchMetrics.MacBookPro14.ScaledBy(1.5);

        Assert.Equal(278, scaled.Width);
        Assert.Equal(48, scaled.Height);
        Assert.Equal(12, scaled.BottomCornerRadius);
    }

    /// <summary>Two arcs of more than half the box would overlap and the silhouette would fold in on itself.</summary>
    [Fact]
    public void NeverLetsTheRadiusExceedHalfTheBox()
    {
        var squat = new NotchMetrics(200, 10, 40).ScaledBy(1.0);

        Assert.Equal(5, squat.BottomCornerRadius);
    }

    [Fact]
    public void NeverScalesAwayToNothing()
    {
        var tiny = NotchMetrics.MacBookPro14.ScaledBy(0.001);

        Assert.Equal(1, tiny.Width);
        Assert.Equal(1, tiny.Height);
    }

    [Fact]
    public void HoldsApplesShareOfScreenWidth()
    {
        Assert.Equal(312, NotchMetrics.ProportionalWidth(2560));
        Assert.Equal(184, NotchMetrics.ProportionalWidth(1512));
    }
}
