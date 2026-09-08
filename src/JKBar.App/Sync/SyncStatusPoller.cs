// Polls the three sync providers off the UI thread and keeps the newest snapshot for the band.
using System.Runtime.Versioning;
using JKBar.Core.Presentation;
using JKBar.Core.Sync;

namespace JKBar.App.Sync;

[SupportedOSPlatform("windows")]
internal sealed class SyncStatusPoller : IDisposable
{
    private readonly ISyncProvider[] _providers =
    [
        new GlobalSecureAccessSyncProvider(),
        new OneDriveSyncProvider(),
        new SyncthingSyncProvider()
    ];

    private IReadOnlyList<SyncProviderSnapshot> _snapshots = [];
    private IReadOnlyList<BandItem> _items = [];

    internal IReadOnlyList<BandItem> Items => _items;

    /// <summary>The readings behind the circles, so a change of state can be announced as well as drawn.</summary>
    internal IReadOnlyList<SyncProviderSnapshot> Snapshots => _snapshots;

    /// <summary>Two of the three read the registry and the event log synchronously, so the poll runs off the UI thread.</summary>
    internal async Task RefreshAsync(CancellationToken cancellationToken)
    {
        _snapshots = await Task.Run(
            async () =>
            {
                var snapshots = new List<SyncProviderSnapshot>(_providers.Length);
                foreach (var provider in _providers)
                {
                    snapshots.Add(await provider.GetSnapshotAsync(cancellationToken).ConfigureAwait(false));
                }

                return (IReadOnlyList<SyncProviderSnapshot>)snapshots;
            },
            cancellationToken);
        _items = SyncStatusSource.Items(_snapshots);
    }

    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            (provider as IDisposable)?.Dispose();
        }
    }
}
