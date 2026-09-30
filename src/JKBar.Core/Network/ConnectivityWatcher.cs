// Announces connectivity only when the verdict changes, so a stable link stays silent.
using JKBar.Core.Alerts;

namespace JKBar.Core.Network;

public sealed class ConnectivityWatcher
{
    private ConnectivityReport _last = ConnectivityReport.Unknown;

    public ConnectivityState State => _last.State;

    public NotchAlert? Observe(ConnectivityReport report)
    {
        if (report.State == ConnectivityState.Unknown)
        {
            return null;
        }

        var previous = _last;
        _last = report;

        // The first reading is the baseline: only a bad one is worth interrupting for.
        if (previous.Signature == report.Signature
            || (previous.State == ConnectivityState.Unknown && report.State == ConnectivityState.Online))
        {
            return null;
        }

        return report.State switch
        {
            ConnectivityState.Offline => new NotchAlert(
                AlertCategory.Network,
                "network.offline",
                ConnectivityVerdict.Describe(report.State),
                Severity: AlertSeverity.Warning),
            ConnectivityState.Partial => new NotchAlert(
                AlertCategory.Network,
                $"network.partial:{string.Join(',', report.Unreachable)}",
                ConnectivityVerdict.Describe(report.State),
                ConnectivityVerdict.DescribeUnreachable(report.Unreachable),
                AlertSeverity.Done),
            _ => new NotchAlert(
                AlertCategory.Network,
                "network.online",
                ConnectivityVerdict.Describe(report.State),
                ConnectivityVerdict.AllReachable,
                AlertSeverity.Done)
        };
    }
}
