// One notification the notch can show, identified by a key so repeats can be collapsed.
namespace JKBar.Core.Alerts;

public enum AlertCategory
{
    Audio,
    Media,
    Power,
    Network,
    System,
    Device,
    Sync,
    JkBar
}

public enum AlertSeverity
{
    Change,
    Done,
    Warning
}

/// <param name="Key">Repeats of the same key refresh the shown alert instead of queueing twice.</param>
/// <param name="Cooldown">Overrides the policy repeat interval, for values the user changes in bursts.</param>
public sealed record NotchAlert(
    AlertCategory Category,
    string Key,
    string Title,
    string? Detail = null,
    AlertSeverity Severity = AlertSeverity.Change,
    TimeSpan? Cooldown = null);
