// What the notch shows while media is playing, kept as state rather than a one-off alert.
namespace JKBar.Core.Presentation;

/// <param name="AppId">The media session's source application, used to find its icon.</param>
public sealed record NowPlaying(string Title, string? Artist, string AppId)
{
    /// <summary>What the notch would read, so a repaint only happens when it actually changes.</summary>
    public string Signature => $"{AppId}\u001f{Title}\u001f{Artist}";
}
