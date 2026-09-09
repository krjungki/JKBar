// Pins the version string's shape, because a stray commit hash there once made JKMon update to itself forever.
using JKBar.Core;

namespace JKBar.Core.Tests;

public class BuildInfoTests
{
    [Fact]
    public void ReportsAVersion()
    {
        Assert.False(string.IsNullOrWhiteSpace(BuildInfo.Version));
    }

    /// <summary>
    /// `IncludeSourceRevisionInInformationalVersion` is off in the version manifest. If that regresses the
    /// version gains a `+commit` suffix, which broke JKMon's updater comparison in 0.2.1.
    /// </summary>
    [Fact]
    public void CarriesNoSourceRevisionSuffix()
    {
        Assert.DoesNotContain('+', BuildInfo.Version);
    }

    [Fact]
    public void MatchesTheVersionManifest()
    {
        Assert.Equal("0.7.0", BuildInfo.Version);
    }
}
