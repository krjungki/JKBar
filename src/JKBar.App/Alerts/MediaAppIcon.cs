// Finds the icon of the app that owns the media session, from its media session identifier.
using System.Runtime.Versioning;
using JKBar.App.Sync;
using JKBar.Core.Presentation;

namespace JKBar.App.Alerts;

[SupportedOSPlatform("windows")]
internal static class MediaAppIcon
{
    /// <summary>The returned bitmap is cached by the resolver, so callers must not dispose it.</summary>
    internal static Bitmap? Resolve(string appId, int pixelSize)
    {
        foreach (var candidate in MediaSessionApp.ProcessCandidates(appId))
        {
            if (ProviderIconResolver.ResolveProcess(candidate, pixelSize) is { } icon)
            {
                return icon;
            }
        }

        return null;
    }
}
