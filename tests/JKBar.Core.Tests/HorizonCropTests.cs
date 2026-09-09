// Pins the vertical crop that puts every measured scenery horizon on the notch's middle row.
using JKBar.Core.Layout;

namespace JKBar.Core.Tests;

public class HorizonCropTests
{
    [Theory]
    [InlineData("summer field", 96, 68, 40, 56)]
    [InlineData("sunset sky", 96, 64, 32, 64)]
    [InlineData("cloud hill", 96, 70, 44, 52)]
    [InlineData("willow lake", 96, 72, 48, 48)]
    [InlineData("tropical coast", 96, 70, 44, 52)]
    public void CentresTheMeasuredHorizon(
        string scene,
        int imageHeight,
        int horizonY,
        int expectedTop,
        int expectedHeight)
    {
        var crop = HorizonCrop.Centered(imageHeight, horizonY);

        Assert.NotEmpty(scene);
        Assert.Equal(expectedTop, crop.Top);
        Assert.Equal(expectedHeight, crop.Height);
        Assert.Equal(crop.Height / 2, horizonY - crop.Top);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(96, 0)]
    [InlineData(96, 96)]
    public void RejectsAnInvalidHorizon(int imageHeight, int horizonY) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => HorizonCrop.Centered(imageHeight, horizonY));
}