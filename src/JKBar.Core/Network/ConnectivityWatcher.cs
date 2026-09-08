// Announces connectivity only when the verdict changes, so a stable link stays silent.
using JKBar.Core.Alerts;

namespace JKBar.Core.Network;

public sealed class ConnectivityWatcher
{
    private ConnectivityState _last = ConnectivityState.Unknown;

    public ConnectivityState State => _last;

    public NotchAlert? Observe(ConnectivityState state)
    {
        if (state == ConnectivityState.Unknown)
        {
            return null;
        }

        var previous = _last;
        _last = state;

        // The first reading is the baseline: only a bad one is worth interrupting for.
        if (previous == state || (previous == ConnectivityState.Unknown && state == ConnectivityState.Online))
        {
            return null;
        }

        return state switch
        {
            ConnectivityState.Offline => new NotchAlert(
                AlertCategory.Network,
                "network.offline",
                ConnectivityVerdict.Describe(state),
                Severity: AlertSeverity.Warning),
            ConnectivityState.Partial => new NotchAlert(
                AlertCategory.Network,
                "network.partial",
                ConnectivityVerdict.Describe(state),
                "한 곳만 응답합니다",
                AlertSeverity.Warning),
            _ => new NotchAlert(
                AlertCategory.Network,
                "network.online",
                ConnectivityVerdict.Describe(state),
                Severity: AlertSeverity.Done)
        };
    }
}
