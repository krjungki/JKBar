// Ported from JKMon (packages/JKMon/src/JKMon.App/Update/UpdateArguments.cs). Keep behaviour changes in sync.
namespace JKBar.App.Update;

/// <summary>
/// The switches the staged copy is started with to replace the installed one. Parsed here rather than in the
/// applier so a malformed command line is rejected before anything on disk is touched.
/// </summary>
internal sealed record UpdateArguments
{
    internal const string ApplySwitch = "--apply-update";
    internal const string CleanupSwitch = "--cleanup-update";

    internal string? SourceDirectory { get; init; }
    internal string? TargetDirectory { get; init; }
    internal string? WorkDirectory { get; init; }
    internal int WaitForProcessId { get; init; }

    internal bool IsComplete =>
        !string.IsNullOrWhiteSpace(SourceDirectory) && !string.IsNullOrWhiteSpace(TargetDirectory);

    internal static UpdateArguments? Parse(string[] arguments)
    {
        if (arguments.Length == 0 || arguments[0] != ApplySwitch)
        {
            return null;
        }

        var parsed = new UpdateArguments();
        for (var i = 1; i + 1 < arguments.Length; i += 2)
        {
            var value = arguments[i + 1];
            parsed = arguments[i] switch
            {
                "--source" => parsed with { SourceDirectory = value },
                "--target" => parsed with { TargetDirectory = value },
                "--work" => parsed with { WorkDirectory = value },
                "--pid" => parsed with { WaitForProcessId = int.TryParse(value, out var pid) ? pid : 0 },
                _ => parsed
            };
        }

        return parsed.IsComplete ? parsed : null;
    }

    /// <summary>The folder a freshly replaced copy should delete on its first run, if it was given one.</summary>
    internal static string? CleanupTarget(string[] arguments)
    {
        for (var i = 0; i + 1 < arguments.Length; i++)
        {
            if (arguments[i] == CleanupSwitch)
            {
                return arguments[i + 1];
            }
        }

        return null;
    }
}
