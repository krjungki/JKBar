// How long each severity stays up and how soon the same alert may return.
namespace JKBar.Core.Alerts;

public sealed record AlertPolicy
{
    public static AlertPolicy Default { get; } = new();

    public TimeSpan ChangeDwell { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan DoneDwell { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan WarningDwell { get; init; } = TimeSpan.FromSeconds(4);

    /// <summary>A repeat of the same key inside this window is dropped so a flapping source cannot hold the notch.</summary>
    public TimeSpan RepeatCooldown { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan DwellFor(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Done => DoneDwell,
        AlertSeverity.Warning => WarningDwell,
        _ => ChangeDwell
    };
}
