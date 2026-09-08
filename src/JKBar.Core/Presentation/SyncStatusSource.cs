// Turns sync provider snapshots into the small lettered circles the band draws.
using System.Drawing;
using JKBar.Core.Sync;

namespace JKBar.Core.Presentation;

public static class SyncStatusSource
{
    public static readonly Color Synchronizing = Color.FromArgb(38, 103, 196);
    public static readonly Color UpToDate = Color.FromArgb(32, 128, 88);
    public static readonly Color Faulted = Color.FromArgb(218, 48, 57);
    public static readonly Color Indeterminate = Color.FromArgb(122, 122, 130);

    /// <summary>A provider that is not running gets no circle, so the band does not carry dead weight.</summary>
    public static BandItem? Item(SyncProviderSnapshot snapshot)
    {
        if (!snapshot.IsVisible)
        {
            return null;
        }

        return new BandItem(
            snapshot.Initial.ToString(),
            [],
            ColourFor(snapshot.State),
            Layout: BandItemLayout.StatusIcon,
            Kind: KindFor(snapshot.ProviderId),
            Badge: BadgeFor(snapshot.State));
    }

    public static BandItemBadge BadgeFor(SyncState state) =>
        state == SyncState.UpToDate ? BandItemBadge.Good : BandItemBadge.Attention;

    public static IReadOnlyList<BandItem> Items(IEnumerable<SyncProviderSnapshot> snapshots) =>
        [.. snapshots.Select(Item).OfType<BandItem>()];

    public static Color ColourFor(SyncState state) => state switch
    {
        SyncState.Synchronizing => Synchronizing,
        SyncState.UpToDate => UpToDate,
        SyncState.Error => Faulted,
        _ => Indeterminate
    };

    public static BandItemKind KindFor(string providerId) => providerId switch
    {
        SyncProviderCatalog.OneDrive => BandItemKind.OneDrive,
        SyncProviderCatalog.Syncthing => BandItemKind.Syncthing,
        SyncProviderCatalog.GlobalSecureAccess => BandItemKind.GlobalSecureAccess,
        _ => BandItemKind.Custom
    };

    public static string? ProviderIdFor(BandItemKind kind) => kind switch
    {
        BandItemKind.OneDrive => SyncProviderCatalog.OneDrive,
        BandItemKind.Syncthing => SyncProviderCatalog.Syncthing,
        BandItemKind.GlobalSecureAccess => SyncProviderCatalog.GlobalSecureAccess,
        _ => null
    };
}
