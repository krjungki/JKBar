// Ported from JKMon (packages/JKMon/src/JKMon.App/Interop/MonitorCatalog.cs). Keep behaviour changes in sync.
using JKBar.Core.Layout;

namespace JKBar.App.Interop;

/// <param name="Label">What the settings list shows, so the user can tell two identical panels apart.</param>
internal sealed record MonitorEntry(string DeviceName, string Label)
{
    public override string ToString() => Label;
}

internal static class MonitorCatalog
{
    /// <summary>Every attached display, in the order Windows reports them.</summary>
    internal static IReadOnlyList<MonitorEntry> All()
    {
        var screens = Screen.AllScreens;
        var entries = new List<MonitorEntry>(screens.Length);

        for (var i = 0; i < screens.Length; i++)
        {
            var bounds = screens[i].Bounds;
            var scale = (int)Math.Round(
                MonitorScale.At(bounds.Left + (bounds.Width / 2), bounds.Top + (bounds.Height / 2)) * 100);

            entries.Add(new MonitorEntry(
                screens[i].DeviceName,
                $"{i + 1} · {bounds.Width}×{bounds.Height} · {scale}%"
                    + (screens[i].Primary ? " · 주 모니터" : string.Empty)));
        }

        return entries;
    }

    internal static NotchGeometry.Rect? PrimaryBounds()
    {
        var screen = Screen.PrimaryScreen ?? Screen.AllScreens.FirstOrDefault();
        if (screen is null)
        {
            return null;
        }

        var bounds = screen.Bounds;
        return new NotchGeometry.Rect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
    }

    /// <summary>
    /// Panel bounds of the named display, not its work area, because the bar hugs the bezel the way a real notch
    /// does. Null when nothing is named or it is unplugged, so the caller can fall back to the Windows primary.
    /// </summary>
    internal static NotchGeometry.Rect? BoundsOf(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return null;
        }

        foreach (var screen in Screen.AllScreens)
        {
            if (!string.Equals(screen.DeviceName, deviceName, StringComparison.Ordinal))
            {
                continue;
            }

            var bounds = screen.Bounds;

            return new NotchGeometry.Rect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        }

        return null;
    }
}
