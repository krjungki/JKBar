// Ported from JKMon (packages/JKMon/src/JKMon.Core/Sync). JKBar alone has the Korean descriptions and stalled peers.
namespace JKBar.Core.Sync;

/// <summary>Completion counters, as carried by a FolderCompletion event.</summary>
public readonly record struct SyncthingCompletion(double Completion, long NeedBytes, long NeedItems, long NeedDeletes)
{
    public long Outstanding => NeedItems + NeedDeletes;
}

/// <summary>What one connected peer still lacks in one folder, with names ready for display.</summary>
public readonly record struct SyncthingPeerNeed(string Device, string Folder, SyncthingCompletion Completion);

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
    public const string AllUpToDate = "모든 폴더가 최신 상태입니다";
    public const string Transferring = "파일을 주고받는 중입니다";
    public const string CheckPeer = "상대 장치의 Syncthing에서 실패 항목을 확인하세요";

    /// <summary>Completion is a float, so outstanding work is judged by the need counters rather than by equality with 100.</summary>
    public static SyncState ToSyncState(SyncthingCompletion completion)
    {
        if (completion.NeedBytes > 0 || completion.NeedItems > 0 || completion.NeedDeletes > 0)
        {
            return SyncState.Synchronizing;
        }

        return completion.Completion >= 99.9995d ? SyncState.UpToDate : SyncState.Synchronizing;
    }

    /// <summary>Fewer outstanding entries, or as many with fewer bytes, is progress.</summary>
    public static bool Improves(SyncthingCompletion current, SyncthingCompletion best) =>
        current.Outstanding < best.Outstanding
        || (current.Outstanding == best.Outstanding && current.NeedBytes < best.NeedBytes);

    /// <summary>What <paramref name="current"/> needs beyond <paramref name="floor"/>, or null when nothing.</summary>
    public static SyncthingCompletion? Above(SyncthingCompletion current, SyncthingCompletion floor)
    {
        var excess = new SyncthingCompletion(
            current.Completion,
            Math.Max(0, current.NeedBytes - floor.NeedBytes),
            Math.Max(0, current.NeedItems - floor.NeedItems),
            Math.Max(0, current.NeedDeletes - floor.NeedDeletes));

        return excess.NeedBytes > 0 || excess.Outstanding > 0 ? excess : null;
    }

    public static IEnumerable<string> DescribeSending(IReadOnlyList<SyncthingPeerNeed> sending) =>
        sending
            .GroupBy(peer => peer.Device, StringComparer.Ordinal)
            .Select(group =>
            {
                var items = group.Sum(peer => peer.Completion.NeedItems + peer.Completion.NeedDeletes);
                return items > 0 ? $"{group.Key}에 {items}건 보내는 중" : $"{group.Key}에 보내는 중";
            });

    public static IEnumerable<string> DescribeStalled(IReadOnlyList<SyncthingPeerNeed> stalled) =>
        stalled.Select(peer => $"{peer.Device} · {peer.Folder}: {Outstanding(peer.Completion)} 진척 없음");

    private static string Outstanding(SyncthingCompletion completion)
    {
        var parts = new List<string>(2);
        if (completion.NeedItems > 0)
        {
            parts.Add($"항목 {completion.NeedItems}건");
        }

        if (completion.NeedDeletes > 0)
        {
            parts.Add($"삭제 {completion.NeedDeletes}건");
        }

        return parts.Count == 0 ? "남은 데이터" : string.Join(", ", parts);
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

    public static IEnumerable<string> DescribeFolders(IReadOnlyList<SyncthingFolderStatus> folders) =>
        folders
            .Where(folder => ToSyncState(folder) != SyncState.UpToDate)
            .Select(folder => $"{folder.Name}: {DescribeFolder(folder)}");

    private static string DescribeFolder(SyncthingFolderStatus folder)
    {
        if (folder.PullErrors > 0)
        {
            return $"받기 오류 {folder.PullErrors}건";
        }

        if (!string.IsNullOrEmpty(folder.State) && !Is(folder.State, SyncthingFolderStatus.IdleState))
        {
            return StateName(folder.State);
        }

        return folder.NeedTotalItems > 0
            ? $"맞지 않는 항목 {folder.NeedTotalItems}건"
            : $"로컬 변경 {folder.ReceiveOnlyChangedFiles}건";
    }

    private static string StateName(string state) => state.ToLowerInvariant() switch
    {
        "scanning" => "검사 중",
        "scan-waiting" => "검사 대기",
        "syncing" => "동기화 중",
        "sync-preparing" => "동기화 준비",
        "sync-waiting" => "동기화 대기",
        "cleaning" => "정리 중",
        "clean-waiting" => "정리 대기",
        SyncthingFolderStatus.PausedState => "일시 중지됨",
        SyncthingFolderStatus.ErrorState => "오류",
        _ => state
    };

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
