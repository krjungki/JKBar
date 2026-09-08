// Decides which watched executables are running, kept apart from the Win32 call that lists them.
using JKBar.Core.Settings;

namespace JKBar.Core.Presentation;

public static class ProcessWatchSource
{
    /// <param name="runningCounts">
    /// How many processes carry each name, as Windows reports them and without the executable extension.
    /// </param>
    public static IReadOnlyList<RunningProcess> Running(
        ProcessWatchSettings settings,
        IReadOnlyDictionary<string, int> runningCounts)
    {
        var normalized = settings.Normalized();
        if (normalized.Items.Length == 0)
        {
            return [];
        }

        var running = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, count) in runningCounts)
        {
            if (!string.IsNullOrWhiteSpace(name) && count > 0)
            {
                running[name.Trim()] = count;
            }
        }

        return
        [
            .. normalized.Items
                .Where(item => running.ContainsKey(item.MatchKey))
                .Select(item => new RunningProcess(item, running[item.MatchKey]))
        ];
    }

    /// <summary>What the band is showing right now, so an unchanged list does not repaint the full-width surface.</summary>
    public static string Signature(IReadOnlyList<RunningProcess> running) =>
        string.Join('|', running.Select(item => $"{item.Watched.MatchKey}:{item.Count}"));
}
