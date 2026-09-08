// Ported from JKMon (packages/JKMon/src/JKMon.Core/Sync). Keep behaviour changes in sync with the original.
using System.Net;
using System.Xml.Linq;

namespace JKBar.Core.Sync;

/// <summary>Endpoint details discovered from the local Syncthing configuration.</summary>
public sealed class SyncthingEndpoint
{
    public required Uri BaseAddress { get; init; }

    /// <summary>Held only for the lifetime of the process; it is never persisted or logged by JKBar.</summary>
    public required string ApiKey { get; init; }
}

public static class SyncthingConfigReader
{
    public static string DefaultConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Syncthing",
        "config.xml");

    /// <summary>Returns null when Syncthing is not configured locally or the GUI is not reachable over loopback.</summary>
    public static SyncthingEndpoint? TryRead(string? configPath = null)
    {
        var path = configPath ?? DefaultConfigPath;
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var gui = XDocument.Load(path).Root?.Element("gui");
            var address = gui?.Element("address")?.Value;
            var apiKey = gui?.Element("apikey")?.Value;
            if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(apiKey))
            {
                return null;
            }

            var scheme = string.Equals(gui?.Attribute("tls")?.Value, "true", StringComparison.OrdinalIgnoreCase)
                ? "https"
                : "http";

            if (!Uri.TryCreate($"{scheme}://{address.Trim()}", UriKind.Absolute, out var uri) || !IsLoopback(uri))
            {
                return null;
            }

            return new SyncthingEndpoint { BaseAddress = uri, ApiKey = apiKey.Trim() };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return null;
        }
    }

    /// <summary>JKBar only ever talks to a Syncthing instance on this machine.</summary>
    public static bool IsLoopback(Uri uri) =>
        uri.IsLoopback || (IPAddress.TryParse(uri.Host, out var ip) && IPAddress.IsLoopback(ip));
}
