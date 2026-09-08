// Pins how a media session identifier is turned into process names to look for.
using JKBar.Core.Presentation;

namespace JKBar.Core.Tests;

public class MediaSessionAppTests
{
    [Fact]
    public void UsesADesktopIdentifierAsIs() =>
        Assert.Equal(["Chrome"], MediaSessionApp.ProcessCandidates("Chrome"));

    [Fact]
    public void StripsTheExecutableExtension() =>
        Assert.Equal(["Spotify"], MediaSessionApp.ProcessCandidates("Spotify.exe"));

    [Fact]
    public void TriesTheEntryPointOfAPackagedAppFirst()
    {
        var candidates = MediaSessionApp.ProcessCandidates("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify").ToArray();

        Assert.Equal("Spotify", candidates[0]);
        Assert.Contains("SpotifyAB.SpotifyMusic", candidates);
        Assert.Contains("SpotifyMusic", candidates);
    }

    [Fact]
    public void ReportsNothingForAnEmptyIdentifier()
    {
        Assert.Empty(MediaSessionApp.ProcessCandidates(null));
        Assert.Empty(MediaSessionApp.ProcessCandidates("   "));
    }
}
