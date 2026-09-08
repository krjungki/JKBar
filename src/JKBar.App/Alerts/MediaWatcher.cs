// Reads the system media session so the notch can name what is playing.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using JKBar.Core.Alerts;
using JKBar.Core.Presentation;
using Windows.Media.Control;

namespace JKBar.App.Alerts;

[SupportedOSPlatform("windows10.0.19041.0")]
internal sealed class MediaWatcher
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private string _playing = string.Empty;
    private bool _unavailable;

    /// <summary>Null when nothing is playing; the notch shows this until playback stops.</summary>
    internal NowPlaying? Current { get; private set; }

    internal async Task<NotchAlert?> ObserveAsync()
    {
        if (_unavailable)
        {
            return null;
        }

        try
        {
            return await ReadAsync().ConfigureAwait(true);
        }
        catch (COMException)
        {
            return null;
        }
        catch (TypeLoadException)
        {
            _unavailable = true;
            return null;
        }
    }

    private async Task<NotchAlert?> ReadAsync()
    {
        _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

        var session = _manager.GetCurrentSession();
        if (session?.GetPlaybackInfo()?.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
        {
            _playing = string.Empty;
            Current = null;
            return null;
        }

        var properties = await session.TryGetMediaPropertiesAsync();
        var title = properties?.Title?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            Current = null;
            return null;
        }

        var artist = properties?.Artist?.Trim();
        Current = new NowPlaying(title, string.IsNullOrEmpty(artist) ? null : artist, session.SourceAppUserModelId ?? string.Empty);

        var track = $"{title}\u001f{artist}";
        if (track == _playing)
        {
            return null;
        }

        _playing = track;
        return new NotchAlert(
            AlertCategory.Media,
            "media.track",
            title,
            string.IsNullOrEmpty(artist) ? null : artist,
            Cooldown: TimeSpan.Zero);
    }
}
