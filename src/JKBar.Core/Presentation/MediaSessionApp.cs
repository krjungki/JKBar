// Turns a media session identifier into the process names its icon might live in.
namespace JKBar.Core.Presentation;

public static class MediaSessionApp
{
    /// <summary>
    /// Identifiers vary: a desktop app reports "Spotify.exe" or "Chrome", a packaged one reports
    /// "Family_hash!App". The shortenings are returned most specific first.
    /// </summary>
    public static IEnumerable<string> ProcessCandidates(string? appId)
    {
        var trimmed = appId?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            yield break;
        }

        var bang = trimmed.IndexOf('!', StringComparison.Ordinal);
        if (bang >= 0 && bang < trimmed.Length - 1)
        {
            // A packaged app names its entry point after the executable far more often than its family does.
            yield return trimmed[(bang + 1)..];
        }

        var head = bang >= 0 ? trimmed[..bang] : trimmed;
        if (head.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            head = head[..^4];
        }

        if (head.Length == 0)
        {
            yield break;
        }

        yield return head;

        var underscore = head.IndexOf('_', StringComparison.Ordinal);
        if (underscore > 0)
        {
            head = head[..underscore];
            yield return head;
        }

        var lastDot = head.LastIndexOf('.');
        if (lastDot > 0 && lastDot < head.Length - 1)
        {
            yield return head[(lastDot + 1)..];
        }
    }
}
