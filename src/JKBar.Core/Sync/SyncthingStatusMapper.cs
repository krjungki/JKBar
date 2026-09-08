// Ported from JKMon (packages/JKMon/src/JKMon.Core/Sync). Keep behaviour changes in sync with the original.
namespace JKBar.Core.Sync;

/// <summary>Aggregate completion payload of GET /rest/db/completion with no folder or device parameter.</summary>
public readonly record struct SyncthingCompletion(double Completion, long NeedBytes, long NeedItems, long NeedDeletes);

/// <summary>Per-folder status, from GET /rest/db/status or the summary carried by a FolderSummary event.</summary>
public readonly record struct SyncthingFolderStatus(
    string Name,
    string State,
    long NeedTotalItems,
    long PullErrors,
    long ReceiveOnlyChangedFiles)
{
    public const string IdleState = "idle";
    public const string PausedState = "paused";
    public const string ErrorState = "error";
}

public static class SyncthingStatusMapper
{
    /// <summary>Completion is a float, so outstanding work is judged by the need counters rather than by equality with 100.</summary>
    public static SyncState ToSyncState(SyncthingCompletion completion)
    {
        if (completion.NeedBytes > 0 || completion.NeedItems > 0 || completion.NeedDeletes > 0)
        {
            return SyncState.Synchronizing;
        }

        return completion.Completion >= 99.9995d ? SyncState.UpToDate : SyncState.Synchronizing;
    }

    public static string Describe(SyncthingCompletion completion, SyncState state) => state switch
    {
        SyncState.UpToDate => "all folders up to date",
        SyncState.Synchronizing => $"{completion.Completion:0.##}% complete, {completion.NeedItems} item(s) pending",
        _ => "state unavailable"
    };

    /// <summary>
    /// The device-less completion call only covers what this machine still needs, so a locally edited file looks
    /// finished there. Outgoing work shows up as the connected peers still needing data from us.
    /// </summary>
    public static SyncState Aggregate(SyncthingCompletion local, IReadOnlyList<SyncthingCompletion> remotes)
    {
        if (ToSyncState(local) == SyncState.Synchronizing)
        {
            return SyncState.Synchronizing;
        }

        foreach (var remote in remotes)
        {
            if (ToSyncState(remote) == SyncState.Synchronizing)
            {
                return SyncState.Synchronizing;
            }
        }

        return SyncState.UpToDate;
    }

    public static string Describe(SyncthingCompletion local, IReadOnlyList<SyncthingCompletion> remotes)
    {
        var receiving = ToSyncState(local) == SyncState.Synchronizing;
        var sendingTo = remotes.Count(remote => ToSyncState(remote) == SyncState.Synchronizing);

        if (!receiving && sendingTo == 0)
        {
            return "all folders up to date";
        }

        var parts = new List<string>(2);
        if (receiving)
        {
            parts.Add($"receiving {local.NeedItems} item(s)");
        }

        if (sendingTo > 0)
        {
            var pending = remotes
                .Where(remote => ToSyncState(remote) == SyncState.Synchronizing)
                .Sum(remote => remote.NeedItems);
            parts.Add($"sending {pending} item(s) to {sendingTo} device(s)");
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Only an idle folder with nothing outstanding counts as up to date. Every other state the daemon reports,
    /// including scanning, syncing, cleaning and paused, means the folder does not yet match the cluster.
    /// </summary>
    public static SyncState ToSyncState(SyncthingFolderStatus folder)
    {
        if (folder.PullErrors > 0 || Is(folder.State, SyncthingFolderStatus.ErrorState))
        {
            return SyncState.Error;
        }

        // An absent state means the daemon told us nothing, so the counters decide rather than forcing a red circle.
        if (!string.IsNullOrEmpty(folder.State) && !Is(folder.State, SyncthingFolderStatus.IdleState))
        {
            return SyncState.Synchronizing;
        }

        return folder.NeedTotalItems > 0 || folder.ReceiveOnlyChangedFiles > 0
            ? SyncState.Synchronizing
            : SyncState.UpToDate;
    }

    public static SyncState AggregateFolders(IReadOnlyList<SyncthingFolderStatus> folders)
    {
        var worst = SyncState.UpToDate;
        foreach (var folder in folders)
        {
            worst = Worse(worst, ToSyncState(folder));
        }

        return worst;
    }

    /// <summary>Error outranks in-progress work so the tooltip names the more serious condition.</summary>
    public static SyncState Worse(SyncState first, SyncState second) => Rank(first) >= Rank(second) ? first : second;

    public static string DescribeFolders(IReadOnlyList<SyncthingFolderStatus> folders)
    {
        var unfinished = folders.Where(folder => ToSyncState(folder) != SyncState.UpToDate).ToList();

        return unfinished.Count == 0
            ? "all folders up to date"
            : string.Join(", ", unfinished.Select(folder => $"{folder.Name} {DescribeFolder(folder)}"));
    }

    private static string DescribeFolder(SyncthingFolderStatus folder)
    {
        if (folder.PullErrors > 0)
        {
            return $"{folder.PullErrors} pull error(s)";
        }

        if (!string.IsNullOrEmpty(folder.State) && !Is(folder.State, SyncthingFolderStatus.IdleState))
        {
            return folder.State;
        }

        return folder.NeedTotalItems > 0
            ? $"{folder.NeedTotalItems} item(s) out of sync"
            : $"{folder.ReceiveOnlyChangedFiles} local change(s)";
    }

    private static int Rank(SyncState state) => state switch
    {
        SyncState.Error => 3,
        SyncState.Synchronizing => 2,
        SyncState.Unknown => 1,
        _ => 0
    };

    private static bool Is(string? state, string expected) =>
        string.Equals(state, expected, StringComparison.OrdinalIgnoreCase);
}
