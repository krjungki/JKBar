// Ported from JKMon (packages/JKMon/src/JKMon.Core/Sync). Keep behaviour changes in sync with the original.
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.Versioning;

namespace JKBar.Core.Sync;

/// <summary>
/// Reads the Global Secure Access client state. The client publishes its tray-icon status to an operational event
/// log that a standard user can read, and it re-publishes on every start, so the newest status event is the current
/// state rather than a stale one.
/// Nothing from the client's registry keys is read: they hold the signed-in UPN and tenant and device identifiers.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GlobalSecureAccessSyncProvider : ISyncProvider
{
    private const string ProcessName = "GlobalSecureAccessClient";
    private const string LogName = "Microsoft-Windows-Global Secure Access Client-Operational";

    private static readonly string StatusQuery =
        $"*[System[({string.Join(" or ", GlobalSecureAccessStatusMapper.StatusEventIds.Select(id => $"EventID={id}"))})]]";

    private readonly Func<bool> _isRunning;
    private readonly Func<GlobalSecureAccessStatus?> _readStatus;

    public GlobalSecureAccessSyncProvider(
        Func<bool>? isRunning = null,
        Func<GlobalSecureAccessStatus?>? readStatus = null)
    {
        _isRunning = isRunning ?? ClientIsRunning;
        _readStatus = readStatus ?? ReadNewestStatus;
    }

    public string ProviderId => SyncProviderCatalog.GlobalSecureAccess;

    public char Initial => 'G';

    public Task<SyncProviderSnapshot> GetSnapshotAsync(CancellationToken cancellationToken) =>
        Task.FromResult(GetSnapshot());

    public SyncProviderSnapshot GetSnapshot()
    {
        if (!_isRunning())
        {
            return SyncProviderSnapshot.Absent(ProviderId, Initial);
        }

        var status = _readStatus();
        if (status is null)
        {
            return new SyncProviderSnapshot(ProviderId, Initial, SyncState.Unknown, "no status reported yet");
        }

        var state = GlobalSecureAccessStatusMapper.ToSyncState(status.Value.EventId);
        return new SyncProviderSnapshot(ProviderId, Initial, state, status.Value.Description);
    }

    private static bool ClientIsRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName(ProcessName);
            foreach (var process in processes)
            {
                process.Dispose();
            }

            return processes.Length > 0;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>A reverse query stops at the first match, which is why reading the current state costs a millisecond.</summary>
    private static GlobalSecureAccessStatus? ReadNewestStatus()
    {
        try
        {
            var query = new EventLogQuery(LogName, PathType.LogName, StatusQuery) { ReverseDirection = true };
            using var reader = new EventLogReader(query);
            using var record = reader.ReadEvent();

            if (record is null)
            {
                return null;
            }

            return new GlobalSecureAccessStatus(
                record.Id,
                GlobalSecureAccessStatusMapper.Describe(record.Id),
                record.TimeCreated);
        }
        catch (Exception ex) when (ex is EventLogNotFoundException or EventLogException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
