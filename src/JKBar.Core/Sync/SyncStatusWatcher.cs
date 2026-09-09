// Turns a sync provider's badge changing colour into something the notch says out loud.
using JKBar.Core.Alerts;
using JKBar.Core.Presentation;

namespace JKBar.Core.Sync;

public sealed class SyncStatusWatcher
{
    /// <summary>Long enough that a provider flipping in and out of sync cannot hold the notch.</summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(2);

    private readonly Dictionary<string, BandItemBadge> _last = new(StringComparer.Ordinal);

    /// <param name="allowsAlert">
    /// Muted results are still tracked, so a separately enabled recovery or attention state can be announced next.
    /// </param>
    public IReadOnlyList<NotchAlert> Observe(
        IEnumerable<SyncProviderSnapshot> snapshots,
        Func<string, BandItemBadge, bool> allowsAlert)
    {
        var alerts = new List<NotchAlert>();

        foreach (var snapshot in snapshots)
        {
            var kind = SyncStatusSource.KindFor(snapshot.ProviderId);
            var badge = SyncStatusSource.BadgeFor(snapshot.State);
            if (!snapshot.IsVisible || kind == BandItemKind.Custom)
            {
                _last.Remove(snapshot.ProviderId);
                continue;
            }

            var known = _last.TryGetValue(snapshot.ProviderId, out var previous);
            _last[snapshot.ProviderId] = badge;

            // The first reading is the baseline; only a change is worth interrupting for.
            if (!known || previous == badge || !allowsAlert(snapshot.ProviderId, badge))
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
