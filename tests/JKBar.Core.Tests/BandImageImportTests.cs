// Regression checks for the copy JKBar keeps of the user's band image.
using JKBar.Core.Settings;

namespace JKBar.Core.Tests;

public class BandImageImportTests
{
    [Fact]
    public void LeavesAnImageThatAlreadyFitsAlone()
    {
        Assert.Equal((64, 64), BandImageImport.Fit(64, 64));
        Assert.Equal((512, 128), BandImageImport.Fit(512, 128));
    }

    [Fact]
    public void ShrinksATallImageToTheBandHeight()
    {
        var (width, height) = BandImageImport.Fit(2000, 4000);

        Assert.Equal(BandImageImport.MaximumHeight, height);
        Assert.Equal(64, width);
    }

    [Fact]
    public void ShrinksAWideBannerToTheWidthLimit()
    {
        var (width, height) = BandImageImport.Fit(4096, 256);

        Assert.Equal(BandImageImport.MaximumWidth, width);
        Assert.Equal(32, height);
    }

    [Fact]
    public void KeepsTheShapeOfWhateverItShrinks()
    {
        var (width, height) = BandImageImport.Fit(1920, 1080);

        Assert.InRange(width / (double)height, 1920 / 1080d - 0.02, 1920 / 1080d + 0.02);
        Assert.True(width <= BandImageImport.MaximumWidth && height <= BandImageImport.MaximumHeight);
    }

    [Fact]
    public void NeverProducesAnEmptyImage()
    {
        var (width, height) = BandImageImport.Fit(4000, 3);

        Assert.True(width >= 1 && height >= 1);
        Assert.Equal((0, 0), BandImageImport.Fit(0, 0));
        Assert.Equal((0, 0), BandImageImport.Fit(-5, 10));
    }

    [Fact]
    public void NamesEachCopyByTheMomentItWasTaken()
    {
        var name = BandImageImport.FileNameFor(new DateTimeOffset(2026, 9, 8, 8, 30, 12, 345, TimeSpan.Zero));

        Assert.Equal("user_image_20260908083012345.png", name);
    }

    [Fact]
    public void ANewerCopySortsAfterAnOlderOne()
    {
        var older = BandImageImport.FileNameFor(new DateTimeOffset(2026, 9, 8, 8, 30, 0, TimeSpan.Zero));
        var newer = BandImageImport.FileNameFor(new DateTimeOffset(2026, 9, 8, 8, 30, 1, TimeSpan.Zero));

        Assert.True(string.CompareOrdinal(older, newer) < 0);
    }

    [Fact]
    public void RecognisesTheCopiesItOwns()
    {
        Assert.True(BandImageImport.IsImportedCopy("C:\\App\\user_image_20260908083012345.png", "C:\\App"));
        Assert.True(BandImageImport.IsImportedCopy("C:\\App\\sub\\..\\user_image_1.png", "C:\\App"));
    }

    [Fact]
    public void DoesNotMistakeTheUsersOwnFileForACopy()
    {
        Assert.False(BandImageImport.IsImportedCopy("C:\\Users\\me\\Downloads\\logo.png", "C:\\App"));
        Assert.False(BandImageImport.IsImportedCopy("C:\\Other\\user_image_1.png", "C:\\App"));
        Assert.False(BandImageImport.IsImportedCopy(null, "C:\\App"));
        Assert.False(BandImageImport.IsImportedCopy("C:\\App\\user_image_1.png", string.Empty));
    }
}
