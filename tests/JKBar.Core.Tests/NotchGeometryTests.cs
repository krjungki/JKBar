// Pins the placement rules the bar depends on, including the clamps that stop a bad width from leaving the screen.
using JKBar.Core.Layout;

namespace JKBar.Core.Tests;

public class NotchGeometryTests
{
    private static readonly NotchGeometry.Rect Screen2560 = new(0, 0, 2560, 1600);

    [Fact]
    public void CentresOnTheTopEdge()
    {
        var placed = NotchGeometry.Place(Screen2560, new NotchMetrics(200, 40, 8), NotchAlignment.Centre);

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

        var placed = NotchGeometry.Place(secondMonitor, NotchMetrics.MacBookPro14, NotchAlignment.Centre);

        Assert.Equal(-200, placed.Top);
    }

    [Theory]
    [InlineData(NotchAlignment.Left, 24, 24)]
    [InlineData(NotchAlignment.Right, 24, 2336)]
    public void HonoursTheEdgeInset(NotchAlignment alignment, int inset, int expectedLeft)
    {
        var placed = NotchGeometry.Place(Screen2560, new NotchMetrics(200, 40, 8), alignment, inset);

        Assert.Equal(expectedLeft, placed.Left);
    }

    [Fact]
    public void IgnoresTheInsetWhenCentred()
    {
        var withInset = NotchGeometry.Place(Screen2560, new NotchMetrics(200, 40, 8), NotchAlignment.Centre, 400);
        var without = NotchGeometry.Place(Screen2560, new NotchMetrics(200, 40, 8), NotchAlignment.Centre);

        Assert.Equal(without, withInset);
    }

    [Fact]
    public void ClampsAWidthWiderThanTheScreen()
    {
        var placed = NotchGeometry.Place(Screen2560, new NotchMetrics(4000, 40, 8), NotchAlignment.Centre);

        Assert.Equal(0, placed.Left);
        Assert.Equal(2560, placed.Right);
    }

    [Fact]
    public void KeepsAnOversizedInsetOnScreen()
    {
        var placed = NotchGeometry.Place(Screen2560, new NotchMetrics(200, 40, 8), NotchAlignment.Right, 9000);

        Assert.Equal(0, placed.Left);
        Assert.Equal(200, placed.Right);
    }
}
