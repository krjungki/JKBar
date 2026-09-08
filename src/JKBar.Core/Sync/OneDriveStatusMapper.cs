// Ported from JKMon (packages/JKMon/src/JKMon.Core/Sync). Keep behaviour changes in sync with the original.
namespace JKBar.Core.Sync;

/// <summary>Values of CF_SYNC_PROVIDER_STATUS as documented for cfapi.h.</summary>
public enum CloudProviderStatus : uint
{
    Disconnected = 0x00000000,
    Idle = 0x00000001,
    PopulateNamespace = 0x00000002,
    PopulateMetadata = 0x00000004,
    PopulateContent = 0x00000008,
    SyncIncremental = 0x00000010,
    SyncFull = 0x00000020,
    ConnectivityLost = 0x00000040,
    Terminated = 0xC0000001,
    Error = 0xC0000002
}

/// <summary>Maps Cloud Filter provider status values to circle states and aggregates multiple sync roots.</summary>
public static class OneDriveStatusMapper
{
    public static SyncState ToSyncState(CloudProviderStatus status) => status switch
    {
        CloudProviderStatus.Idle => SyncState.UpToDate,
        CloudProviderStatus.PopulateNamespace
            or CloudProviderStatus.PopulateMetadata
            or CloudProviderStatus.PopulateContent
            or CloudProviderStatus.SyncIncremental
            or CloudProviderStatus.SyncFull => SyncState.Synchronizing,
        CloudProviderStatus.Error or CloudProviderStatus.Terminated => SyncState.Error,
        CloudProviderStatus.Disconnected or CloudProviderStatus.ConnectivityLost => SyncState.Unknown,
        _ => SyncState.Unknown
    };

    /// <summary>Green only when every sync root is up to date; any root still working wins over an unknown root.</summary>
    public static SyncState Aggregate(IReadOnlyCollection<SyncState> roots)
    {
        if (roots.Count == 0)
        {
            return SyncState.Absent;
        }

        if (roots.Contains(SyncState.Error))
        {
            return SyncState.Error;
        }

        if (roots.Contains(SyncState.Synchronizing))
        {
            return SyncState.Synchronizing;
        }

        if (roots.Contains(SyncState.Unknown))
        {
            return SyncState.Unknown;
        }

        return roots.All(r => r == SyncState.UpToDate) ? SyncState.UpToDate : SyncState.Unknown;
    }
}
