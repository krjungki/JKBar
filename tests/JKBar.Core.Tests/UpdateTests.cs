using JKBar.Core.Settings;
using JKBar.Core.Update;

namespace JKBar.Core.Tests;

public sealed class ReleaseVersionTests
{
    [Theory]
    [InlineData("0.5.0", 0, 5, 0, "")]
    [InlineData("v1.2.3", 1, 2, 3, "")]
    [InlineData("  2.0.1-beta.2  ", 2, 0, 1, "beta.2")]
    [InlineData("1.0.0+build7", 1, 0, 0, "")]
    public void ReadsWhatAReleaseIsCalled(string text, int major, int minor, int patch, string suffix)
    {
        Assert.True(ReleaseVersion.TryParse(text, out var version));
        Assert.Equal(new ReleaseVersion(major, minor, patch, suffix), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("one.two.three")]
    [InlineData("-1.0.0")]
    public void RefusesAnythingItCannotCompare(string? text)
    {
        Assert.False(ReleaseVersion.TryParse(text, out var version));
        Assert.Equal(ReleaseVersion.Zero, version);
    }

    [Fact]
    public void OrdersByNumberFirst()
    {
        Assert.True(Parse("0.5.1") > Parse("0.5.0"));
        Assert.True(Parse("0.6.0") > Parse("0.5.9"));
        Assert.True(Parse("1.0.0") > Parse("0.99.99"));
    }

    [Fact]
    public void CountsAReleaseAboveItsOwnPrereleases()
    {
        Assert.True(Parse("1.0.0") > Parse("1.0.0-rc.1"));
        Assert.True(Parse("1.0.0-rc.2") > Parse("1.0.0-rc.1"));
    }

    [Fact]
    public void NeverOffersAnOlderBuildAsAnUpdate()
    {
        var running = Parse("0.5.0");

        Assert.False(Parse("0.4.9") > running);
        Assert.False(Parse("0.5.0") > running);
        Assert.True(Parse("0.5.1") > running);
    }

    [Fact]
    public void ReadsBackWhatItPrints()
    {
        Assert.Equal("0.5.0", Parse("0.5.0").ToString());
        Assert.Equal("1.0.0-rc.1", Parse("1.0.0-rc.1").ToString());
    }

    private static ReleaseVersion Parse(string text)
    {
        Assert.True(ReleaseVersion.TryParse(text, out var version));

        return version;
    }
}

public sealed class ReleaseChecksumsTests
{
    private const string Published = """
        SHA-256 checksums for JKBar 0.5.0 (win-x64).

        AA11BB22CC33DD44EE55FF66007788990011223344556677889900AABBCCDDEE  JKBar-0.5.0-win-x64.zip

        Files inside the archive:

        1122334455667788990011223344556677889900112233445566778899001122  JKBar.exe
        not-a-hash  ignored.txt
        """;

    [Fact]
    public void ReadsThePublishedHashes()
    {
        var parsed = ReleaseChecksums.Parse(Published);

        Assert.Equal(2, parsed.Count);
        Assert.Equal("AA11BB22CC33DD44EE55FF66007788990011223344556677889900AABBCCDDEE", parsed["JKBar-0.5.0-win-x64.zip"]);
        Assert.True(parsed.ContainsKey("JKBar.exe"));
    }

    [Fact]
    public void IgnoresTheProseAroundThem()
    {
        Assert.False(ReleaseChecksums.Parse(Published).ContainsKey("ignored.txt"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>404</html>")]
    public void ReadsNothingOutOfSomethingThatIsNotAChecksumFile(string content)
    {
        Assert.Empty(ReleaseChecksums.Parse(content));
    }

    [Fact]
    public void MatchesRegardlessOfCase()
    {
        Assert.True(ReleaseChecksums.Matches(
            "1122334455667788990011223344556677889900112233445566778899001122",
            "1122334455667788990011223344556677889900112233445566778899001122".ToLowerInvariant()));
    }

    [Theory]
    [InlineData(null, "1122334455667788990011223344556677889900112233445566778899001122")]
    [InlineData("1122", "1122")]
    [InlineData("1122334455667788990011223344556677889900112233445566778899001122", "")]
    public void RefusesAnythingThatIsNotAFullMatch(string? expected, string? actual)
    {
        Assert.False(ReleaseChecksums.Matches(expected, actual));
    }

    [Fact]
    public void HashesTheSameBytesTheSameWay()
    {
        using var stream = new MemoryStream("JKBar"u8.ToArray());

        Assert.Equal(64, ReleaseChecksums.HashOf(stream).Length);
    }
}

public sealed class UpdateScheduleTests
{
    private static readonly DateTimeOffset Started = new(2026, 9, 8, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NeverContactsAnyoneWhileTurnedOff()
    {
        Assert.False(UpdateSchedule.IsDue(
            UpdateCheckFrequency.Never, default, Started, Started.AddYears(1), true, false));
    }

    [Fact]
    public void ChecksOnceAtStartupWhenAskedTo()
    {
        Assert.True(UpdateSchedule.IsDue(
            UpdateCheckFrequency.Daily, Started, Started, Started, true, false));
        Assert.False(UpdateSchedule.IsDue(
            UpdateCheckFrequency.Daily, Started, Started, Started, true, true));
    }

    [Fact]
    public void WaitsOutTheIntervalBetweenChecks()
    {
        Assert.False(UpdateSchedule.IsDue(
            UpdateCheckFrequency.Daily, Started, Started, Started.AddHours(23), false, true));
        Assert.True(UpdateSchedule.IsDue(
            UpdateCheckFrequency.Daily, Started, Started, Started.AddHours(25), false, true));
    }

    [Fact]
    public void WaitsLongerWhenAskedForWeekly()
    {
        Assert.False(UpdateSchedule.IsDue(
            UpdateCheckFrequency.Weekly, Started, Started, Started.AddDays(6), false, true));
        Assert.True(UpdateSchedule.IsDue(
            UpdateCheckFrequency.Weekly, Started, Started, Started.AddDays(8), false, true));
    }

    [Fact]
    public void ChecksOnceWhenItNeverHasBefore()
    {
        Assert.True(UpdateSchedule.IsDue(
            UpdateCheckFrequency.Weekly, default, default, Started, false, true));
    }

    [Fact]
    public void RecoversFromAClockThatMovedBackwards()
    {
        Assert.True(UpdateSchedule.IsDue(
            UpdateCheckFrequency.Daily, Started, Started, Started.AddDays(-3), false, true));
    }
}

public sealed class StagingPathsTests
{
    [Fact]
    public void KeepsItsWorkUnderAFolderItCanRecognise()
    {
        var root = StagingPaths.RootFor(@"C:\Temp", new ReleaseVersion(0, 5, 1, string.Empty));

        Assert.Equal(@"C:\Temp\JKBar.update.0.5.1", root);
        Assert.True(StagingPaths.IsStagingRoot(root));
        Assert.True(StagingPaths.IsStagingRoot(root + @"\"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"C:\Users\jungki\Documents")]
    [InlineData(@"C:\Temp\JKBar.update.")]
    [InlineData(@"C:\Temp\JKMon.update.0.5.1")]
    public void RefusesToTreatAnythingElseAsItsOwn(string? path)
    {
        Assert.False(StagingPaths.IsStagingRoot(path));
    }
}

public sealed class UpdateSettingsTests
{
    [Fact]
    public void ChecksNothingUntilTheUserAsksForIt()
    {
        var settings = new JkBarSettings().Normalized().Update;

        Assert.Equal(UpdateCheckFrequency.Never, settings.Check);
        Assert.False(settings.CheckOnStartup);
        Assert.Equal(default, settings.LastCheckUtc);
    }

    [Fact]
    public void FallsBackToOffForAFrequencyItDoesNotKnow()
    {
        Assert.Equal(
            UpdateCheckFrequency.Never,
            new UpdateSettings { Check = (UpdateCheckFrequency)99 }.Normalized().Check);
    }
}
