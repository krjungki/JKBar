// Turns a sync provider's badge changing colour into something the notch says out loud.
using JKBar.Core.Alerts;
using JKBar.Core.Presentation;

namespace JKBar.Core.Sync;

public sealed class SyncStatusWatcher
{
    /// <summary>Long enough that a provider flipping in and out of sync cannot hold the notch.</summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(2);

    private readonly Dictionary<string, BandItemBadge> _last = new(StringComparer.Ordinal);

    /// <param name="isVisible">
    /// A provider the user unchecked is forgotten rather than tracked, so turning it back on starts from a fresh
    /// baseline instead of announcing whatever changed while it was out of sight.
    /// </param>
    public IReadOnlyList<NotchAlert> Observe(
        IEnumerable<SyncProviderSnapshot> snapshots,
        Func<BandItemKind, bool> isVisible)
    {
        var alerts = new List<NotchAlert>();

        foreach (var snapshot in snapshots)
        {
            var kind = SyncStatusSource.KindFor(snapshot.ProviderId);
            if (!snapshot.IsVisible || kind == BandItemKind.Custom || !isVisible(kind))
            {
                _last.Remove(snapshot.ProviderId);
                continue;
            }

            var badge = SyncStatusSource.BadgeFor(snapshot.State);
            var known = _last.TryGetValue(snapshot.ProviderId, out var previous);
            _last[snapshot.ProviderId] = badge;

            // The first reading is the baseline; only a change is worth interrupting for.
            if (!known || previous == badge)
            {
                continue;
            }

            alerts.Add(Alert(snapshot, badge));
        }

        return alerts;
    }

    private static NotchAlert Alert(SyncProviderSnapshot snapshot, BandItemBadge badge)
    {
        var good = badge == BandItemBadge.Good;

        return new NotchAlert(
            AlertCategory.Sync,
            $"sync.{snapshot.ProviderId}.{(good ? "good" : "attention")}",
            $"{SyncProviderCatalog.DisplayName(snapshot.ProviderId)} {(good ? "정상" : "주의")}",
            Describe(snapshot.State),
            good ? AlertSeverity.Done : AlertSeverity.Warning,
            Cooldown);
    }

    public static string Describe(SyncState state) => state switch
    {
        SyncState.Synchronizing => "동기화 중입니다",
        SyncState.UpToDate => "최신 상태입니다",
        SyncState.Error => "오류가 보고되었습니다",
        _ => "상태를 확인할 수 없습니다"
    };

    /// <summary>Which provider an alert came from, so the notch can show that application's own icon.</summary>
    public static string? ProviderIdOf(NotchAlert alert) =>
        alert.Category == AlertCategory.Sync && alert.Key.Split('.') is [_, { Length: > 0 } providerId, ..]
            ? providerId
            : null;
}
