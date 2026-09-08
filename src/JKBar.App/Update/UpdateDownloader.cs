// Ported from JKMon (packages/JKMon/src/JKMon.App/Update/UpdateDownloader.cs). Keep behaviour changes in sync.
using System.IO.Compression;
using JKBar.Core.Update;
using IoPath = System.IO.Path;

namespace JKBar.App.Update;

internal sealed record StagedUpdate(ReleaseVersion Version, string StagedDirectory, string WorkDirectory);

internal sealed class UpdateDownloader(HttpClient http)
{
    internal const string ExecutableName = "JKBar.exe";

    /// <summary>
    /// Downloads the release archive and refuses it unless it matches the SHA-256 published beside it. Nothing on
    /// disk outside the temporary folder is touched here.
    /// </summary>
    internal async Task<StagedUpdate?> TryStageAsync(UpdateInfo info, CancellationToken cancellationToken)
    {
        var work = StagingPaths.RootFor(IoPath.GetTempPath(), info.Version);

        try
        {
            TryDelete(work);
            Directory.CreateDirectory(work);

            var checksums = ReleaseChecksums.Parse(
                await http.GetStringAsync(info.ChecksumUrl, cancellationToken).ConfigureAwait(false));
            if (!checksums.TryGetValue(info.PackageName, out var expectedArchive))
            {
                return null;
            }

            var archive = IoPath.Combine(work, info.PackageName);
            await DownloadAsync(info.PackageUrl, archive, cancellationToken).ConfigureAwait(false);
            if (!Verify(archive, expectedArchive))
            {
                TryDelete(work);
                return null;
            }

            var staged = IoPath.Combine(work, StagingPaths.StagedFolderName);
            ZipFile.ExtractToDirectory(archive, staged);

            var executable = IoPath.Combine(staged, ExecutableName);
            if (!File.Exists(executable))
            {
                TryDelete(work);
                return null;
            }

            // The archive hash already covers this, but the executable is what will actually run.
            if (checksums.TryGetValue(ExecutableName, out var expectedExecutable)
                && !Verify(executable, expectedExecutable))
            {
                TryDelete(work);
                return null;
            }

            File.Delete(archive);

            return new StagedUpdate(info.Version, staged, work);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or IOException
            or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            TryDelete(work);
            return null;
        }
    }

    /// <summary>Only ever used on this app's own staging folders, so a wrong argument cannot erase anything else.</summary>
    internal static void TryDelete(string? path)
    {
        if (!StagingPaths.IsStagingRoot(path) || !Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A leftover temporary folder is harmless; Windows clears it eventually.
        }
    }

    private async Task DownloadAsync(Uri url, string destination, CancellationToken cancellationToken)
    {
        using var response = await http
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var file = File.Create(destination);
        await source.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
    }

    private static bool Verify(string path, string expected)
    {
        using var stream = File.OpenRead(path);

        return ReleaseChecksums.Matches(expected, ReleaseChecksums.HashOf(stream));
    }
}
