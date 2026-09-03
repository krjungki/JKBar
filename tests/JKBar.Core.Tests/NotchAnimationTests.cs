// Pins the expand/collapse motion: the endpoints must be exact or the bar settles a pixel off its resting size.
using JKBar.Core.Layout;

namespace JKBar.Core.Tests;

public class NotchAnimationTests
{
    private static readonly NotchMetrics Collapsed = new(200, 32, 8);

    [Fact]
    public void StartsExactlyAtTheSourceSize()
    {
        Assert.Equal(Collapsed, NotchAnimation.Between(Collapsed, Collapsed.Expanded(), 0d));
    }

    [Fact]
    public void EndsExactlyAtTheTargetSize()
    {
        var expanded = Collapsed.Expanded();

        Assert.Equal(expanded, NotchAnimation.Between(Collapsed, expanded, 1d));
    }

    /// <summary>A timer tick can land past the end; the shape must not overshoot into a larger panel.</summary>
    [Fact]
    public void ClampsProgressBeyondTheEnd()
    {
        var expanded = Collapsed.Expanded();

        Assert.Equal(expanded, NotchAnimation.Between(Collapsed, expanded, 4d));
    }

    [Fact]
    public void DeceleratesRatherThanRunningLinear()
    {
        var half = NotchAnimation.EaseOutCubic(0.5d);

        Assert.True(half > 0.5d, $"expected the curve to be past halfway at t=0.5, was {half}");
    }

    [Fact]
    public void MovesEveryDimensionTogether()
    {
        var expanded = Collapsed.Expanded();
        var middle = NotchAnimation.Between(Collapsed, expanded, 0.5d);

        Assert.InRange(middle.Width, Collapsed.Width + 1, expanded.Width - 1);
        Assert.InRange(middle.Height, Collapsed.Height + 1, expanded.Height - 1);
        Assert.InRange(middle.BottomCornerRadius, Collapsed.BottomCornerRadius + 1, expanded.BottomCornerRadius - 1);
    }

    [Fact]
    public void ExpandsInBothAxes()
    {
        var expanded = Collapsed.Expanded();

        Assert.Equal(400, expanded.Width);
        Assert.Equal(96, expanded.Height);
        Assert.Equal(27, expanded.BottomCornerRadius);
    }
}
