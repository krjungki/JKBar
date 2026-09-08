// Pins the drive bitmask reading: Windows sends one bit per drive letter, starting at A.
using JKBar.Core.Devices;

namespace JKBar.Core.Tests;

public class VolumeChangeTests
{
    [Theory]
    [InlineData(1u, 'A')]
    [InlineData(1u << 4, 'E')]
    [InlineData(1u << 25, 'Z')]
    public void ReadsOneBitPerDriveLetter(uint mask, char expected) =>
        Assert.Equal([expected], VolumeChange.Letters(mask));

    [Fact]
    public void ReadsEveryDriveInOneBroadcast() =>
        Assert.Equal(['D', 'F'], VolumeChange.Letters((1u << 3) | (1u << 5)));

    [Fact]
    public void ReportsNothingForAnEmptyMask() =>
        Assert.Empty(VolumeChange.Letters(0));

    [Fact]
    public void IgnoresBitsBeyondTheDriveLetters() =>
        Assert.Empty(VolumeChange.Letters(1u << 26));

    [Fact]
    public void KeepsArrivalAndRemovalApartSoOneDoesNotSwallowTheOther() =>
        Assert.NotEqual(VolumeChange.Arrived('E').Key, VolumeChange.Removed('E').Key);
}
