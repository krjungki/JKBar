// Ported from JKMon (packages/JKMon/src/JKMon.Core/Sync). Keep behaviour changes in sync with the original.
namespace JKBar.Core.Sync;

/// <summary>A pollable sync provider. Implementations must never throw; they report failures as state instead.</summary>
public interface ISyncProvider
{
    string ProviderId { get; }

    char Initial { get; }

    Task<SyncProviderSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
}
