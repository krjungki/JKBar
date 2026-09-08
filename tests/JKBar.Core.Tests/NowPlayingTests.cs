// Pins the repaint signature that keeps the notch from redrawing while the same track plays.
using JKBar.Core.Presentation;

namespace JKBar.Core.Tests;

public class NowPlayingTests
{
    [Fact]
    public void HoldsTheSameSignatureForTheSameTrack()
    {
        var first = new NowPlaying("Song", "Artist", "Spotify.exe");
        var second = new NowPlaying("Song", "Artist", "Spotify.exe");

        Assert.Equal(first.Signature, second.Signature);
    }

    [Fact]
    public void ChangesSignatureWhenTheTrackChanges()
    {
        var first = new NowPlaying("Song", "Artist", "Spotify.exe");

        Assert.NotEqual(first.Signature, (first with { Title = "Next" }).Signature);
        Assert.NotEqual(first.Signature, (first with { Artist = "Other" }).Signature);
    }

    [Fact]
    public void ChangesSignatureWhenAnotherAppTakesOver()
    {
        var first = new NowPlaying("Song", "Artist", "Spotify.exe");

        Assert.NotEqual(first.Signature, (first with { AppId = "Chrome" }).Signature);
    }

    [Fact]
    public void SeparatesFieldsSoATitleCannotImpersonateAnArtist()
    {
        var split = new NowPlaying("A", "B", "app");
        var joined = new NowPlaying("A\u001fB", null, "app");

        Assert.NotEqual(split.Signature, joined.Signature);
    }
}
