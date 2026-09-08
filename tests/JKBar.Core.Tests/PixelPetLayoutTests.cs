using JKBar.Core.Presentation;

namespace JKBar.Core.Tests;

public sealed class PixelPetLayoutTests
{
    [Theory]
    [InlineData(178, 22)]
    [InlineData(311, 56)]
    public void DrawsSomethingWhereverItFits(int width, int height)
    {
        Assert.True(PixelPetLayout.Fits(width, height));
        Assert.True(PixelPetLayout.Scale(width, height) >= 1);
    }

    [Theory]
    [InlineData(311, 21)]
    [InlineData(23, 56)]
    public void StaysAwayFromANotchTooSmallToHoldIt(int width, int height)
    {
        Assert.False(PixelPetLayout.Fits(width, height));
    }

    [Fact]
    public void KeepsTheSameShareOfEveryBarHeight()
    {
        int[] heights = [32, 40, 48, 56, 70];

        var shares = heights
            .Select(height => PixelPetSprites.Height * PixelPetLayout.Scale(311, height) / height)
            .ToArray();

        Assert.All(shares, share => Assert.InRange(share, 0.78, 0.86));
    }

    [Fact]
    public void GrowsWithTheBarRatherThanInSteps()
    {
        var small = PixelPetLayout.Scale(311, 40);
        var large = PixelPetLayout.Scale(311, 56);

        Assert.True(large > small);
        Assert.Equal(56d / 40d, large / small, 2);
    }

    [Fact]
    public void IsBiggerThanTheWholePixelSizingItReplaced()
    {
        int[] heights = [32, 40, 48, 56];

        Assert.All(heights, height =>
        {
            var whole = Math.Max(1, (height - Math.Max(8, height / 5)) / PixelPetSprites.Height);
            Assert.True(PixelPetLayout.Scale(311, height) > whole);
        });
    }

    [Fact]
    public void NarrowsToFitANotchThatIsWideEnoughButShort()
    {
        // A short, narrow notch is limited by its width, not its height.
        var scale = PixelPetLayout.Scale(40, 200);

        Assert.Equal((40 - PixelPetLayout.SideMargin) / (double)PixelPetSprites.Width, scale, 6);
    }
}
