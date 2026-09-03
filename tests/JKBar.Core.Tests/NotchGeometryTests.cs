// Pins the placement rules the bar depends on, including the clamp that stops a bad width from leaving the screen.
using JKBar.Core.Layout;

namespace JKBar.Core.Tests;

public class NotchGeometryTests
{
    private static readonly NotchGeometry.Rect Screen2560 = new(0, 0, 2560, 1600);

    [Fact]
    public void CentresOnTheTopEdge()
    {
        var placed = NotchGeometry.Place(Screen2560, new NotchMetrics(200, 40, 8));

        Assert.Equal(1180, placed.Left);
        Assert.Equal(1380, placed.Right);
        Assert.Equal(0, placed.Top);
        Assert.Equal(40, placed.Bottom);
    }

    /// <summary>A notch is a hole in the bezel, so it hugs the panel edge even when a taskbar is docked at the top.</summary>
    [Fact]
    public void SitsAtTheScreenTopNotTheWorkArea()
    {
        var secondMonitor = new NotchGeometry.Rect(-1920, -200, 0, 880);

        var placed = NotchGeometry.Place(secondMonitor, NotchMetrics.MacBookPro14);

        Assert.Equal(-200, placed.Top);
    }

    /// <summary>A monitor left of the primary has negative coordinates, where centring arithmetic is easy to get wrong.</summary>
    [Fact]
    public void CentresOnAMonitorWithANegativeOrigin()
    {
        var secondMonitor = new NotchGeometry.Rect(-1920, 0, 0, 1080);

        var placed = NotchGeometry.Place(secondMonitor, new NotchMetrics(200, 40, 8));

        Assert.Equal(-1060, placed.Left);
        Assert.Equal(-860, placed.Right);
    }

    [Fact]
    public void ClampsAWidthWiderThanTheScreen()
    {
        var placed = NotchGeometry.Place(Screen2560, new NotchMetrics(4000, 40, 8));

        Assert.Equal(0, placed.Left);
        Assert.Equal(2560, placed.Right);
    }
}
