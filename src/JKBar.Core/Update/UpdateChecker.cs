// Ported from JKMon (packages/JKMon/src/JKMon.Core/Update/UpdateChecker.cs). Keep behaviour changes in sync.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKBar.Core.Update;

/// <param name="PackageName">The archive's own name, which is also the key its checksum is published under.</param>
public readonly record struct UpdateInfo(
    ReleaseVersion Version,
    Uri PackageUrl,
    Uri ChecksumUrl,
    string PackageName);

public sealed class UpdateChecker(HttpClient http, string? repository = null, TimeProvider? time = null)
{
    public const string DefaultRepository = "krjungki/JKBar";

    private readonly string _repository = string.IsNullOrWhiteSpace(repository) ? DefaultRepository : repository;
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public Uri LatestDownloadBase => new($"https://github.com/{_repository}/releases/latest/download/");

    public Uri ReleasesPage => new($"https://github.com/{_repository}/releases/latest");

    /// <summary>
    /// The permalink is served through a CDN that ignores request cache headers, so a fixed URL keeps answering
    /// with the previous release's manifest for a while after publishing. The artifacts themselves are versioned
    /// in their names, so only this one file needs a unique query to defeat the cache.
    /// </summary>
    public Uri VersionManifestUrl() =>
        new(LatestDownloadBase, $"version.json?t={_time.GetUtcNow().ToUnixTimeMilliseconds()}");

    public string PackageNameFor(ReleaseVersion version) => $"JKBar-{version}-win-x64.zip";

    /// <returns>Null when GitHub could not be reached or answered with something this build cannot read.</returns>
    public async Task<UpdateInfo?> TryGetLatestAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(VersionManifestUrl(), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var payload = JsonSerializer.Deserialize<VersionPayload>(json);
            if (payload is null || !ReleaseVersion.TryParse(payload.Version, out var version))
            {
                return null;
            }

            var package = PackageNameFor(version);

            return new UpdateInfo(
                version,
                new Uri(LatestDownloadBase, package),
                new Uri(LatestDownloadBase, "SHA256SUMS.txt"),
                package);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException
            or JsonException or UriFormatException)
        {
            return null;
        }
    }

    private sealed record VersionPayload
    {
        [JsonPropertyName("version")]
        public string? Version { get; init; }
    }
}
