// Executables the user wants the band to show an icon for while they are running.
using IoPath = System.IO.Path;

namespace JKBar.Core.Settings;

/// <param name="Name">As the user typed it; Windows reports processes without the extension, so both forms match.</param>
/// <param name="Path">
/// Where the icon is read from. Empty means the icon comes from whichever copy is running instead, which is what
/// an entry registered by name alone relies on.
/// </param>
public sealed record WatchedProcess
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public bool ShowProcessCount { get; init; } = true;

    /// <summary>What a running process has to be called to count as this entry.</summary>
    public string MatchKey =>
        Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? Name[..^4] : Name;

    /// <summary>True when no executable was pointed at, so the icon has to come from the running process.</summary>
    public bool ByNameOnly => Path.Length == 0;

    public WatchedProcess Normalized()
    {
        var path = (Path ?? string.Empty).Trim().Trim('"');
        var name = (Name ?? string.Empty).Trim();
        if (name.Length == 0 && path.Length > 0)
        {
            name = SafeFileName(path);
        }

        return this with { Name = name, Path = path };
    }

    private static string SafeFileName(string path)
    {
        try
        {
            return IoPath.GetFileName(path);
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }
}

public sealed record ProcessWatchSettings
{
    /// <summary>The band is one strip wide, so the list is capped rather than allowed to push the readouts off.</summary>
    public const int MaximumItems = 8;

    public WatchedProcess[] Items { get; init; } = [];

    public ProcessWatchSettings Normalized() => this with
    {
        Items = (Items ?? [])
            .Select(item => (item ?? new WatchedProcess()).Normalized())
            .Where(item => item.MatchKey.Length > 0)
            .DistinctBy(item => item.MatchKey, StringComparer.OrdinalIgnoreCase)
            .Take(MaximumItems)
            .ToArray()
    };
}
