// Pins how the band divides around the notch, where an off-by-one leaves content sliding under the bar.
using JKBar.Core.Layout;

namespace JKBar.Core.Tests;

public class BandLayoutTests
{
    private static readonly NotchGeometry.Rect Band = new(0, 0, 2560, 56);
    private static readonly NotchGeometry.Rect Notch = new(1124, 0, 1436, 56);

    [Fact]
    public void KeepsBothSlotsClearOfTheNotch()
    {
        var slots = BandLayout.Divide(Band, Notch, 12);

        Assert.Equal(12, slots.Left.Left);
        Assert.Equal(1112, slots.Left.Right);
        Assert.Equal(1448, slots.Right.Left);
        Assert.Equal(2548, slots.Right.Right);
    }

    [Fact]
    public void GivesBothSlotsTheFullBandHeight()
    {
        var slots = BandLayout.Divide(Band, Notch, 12);

        Assert.Equal(56, slots.Left.Height);
        Assert.Equal(56, slots.Right.Height);
    }

    /// <summary>An alert panel can be wider than the band's usable space; the answer is no room, not a negative one.</summary>
    [Fact]
    public void ReportsEmptySlotsWhenTheNotchFillsTheBand()
    {
        var slots = BandLayout.Divide(Band, new NotchGeometry.Rect(0, 0, 2560, 56), 12);

        Assert.Equal(0, slots.Left.Width);
        Assert.Equal(0, slots.Right.Width);
    }

    [Fact]
    public void NeverRunsPastTheBandWithGenerousPadding()
    {
        var slots = BandLayout.Divide(Band, Notch, 4000);

        Assert.InRange(slots.Left.Left, Band.Left, Band.Right);
        Assert.InRange(slots.Right.Right, Band.Left, Band.Right);
        Assert.True(slots.Left.Width >= 0);
        Assert.True(slots.Right.Width >= 0);
    }
}
