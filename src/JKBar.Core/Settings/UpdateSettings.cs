// How often the app may look for a new release, and when it last did.
using JKBar.Core.Update;

namespace JKBar.Core.Settings;

public sealed record UpdateSettings
{
    /// <summary>How often the app may contact GitHub on its own. Never disables every automatic check.</summary>
    public UpdateCheckFrequency Check { get; init; } = UpdateCheckFrequency.Never;

    public bool CheckOnStartup { get; init; }

    /// <summary>Default means no check has run yet.</summary>
    public DateTimeOffset LastCheckUtc { get; init; }

    public UpdateSettings Normalized() => this with
    {
        Check = Enum.IsDefined(Check) ? Check : UpdateCheckFrequency.Never
    };
}
