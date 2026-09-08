// Ported from JKMon (packages/JKMon/src/JKMon.Core/Sync). Keep behaviour changes in sync with the original.
namespace JKBar.Core.Sync;

/// <summary>Decides which Syncthing events mean a transfer or scan is under way.</summary>
public static class SyncthingEventFilter
{
    private static readonly HashSet<string> ActivityTypes = new(StringComparer.Ordinal)
    {
        "ItemStarted",
        "ItemFinished",
        "LocalIndexUpdated",
        "RemoteIndexUpdated",
        "DownloadProgress",
        "RemoteDownloadProgress",
        "FolderScanProgress"
    };

    /// <summary>
    /// <paramref name="stateTo"/> carries the folder's new state for StateChanged events; every state other than
    /// idle means work is happening.
    /// </summary>
    public static bool IndicatesActivity(string? type, string? stateTo)
    {
        if (string.IsNullOrEmpty(type))
        {
            return false;
        }

        if (type == "StateChanged")
        {
            return !string.IsNullOrEmpty(stateTo) && !string.Equals(stateTo, "idle", StringComparison.Ordinal);
        }

        return ActivityTypes.Contains(type);
    }
}
