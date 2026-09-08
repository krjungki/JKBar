// Where a watched-process icon was drawn, so a click on the band can be matched back to its application.
using System.Drawing;
using JKBar.Core.Settings;

namespace JKBar.Core.Presentation;

public sealed record ProcessIcon(WatchedProcess Process, Rectangle Bounds);

public enum ProcessActivation
{
    None,
    Focus,
    Launch
}

public static class ProcessIconHitTest
{
    /// <param name="point">In the same coordinates the bounds were recorded in, which is the band's client area.</param>
    public static WatchedProcess? At(IReadOnlyList<ProcessIcon> icons, Point point)
    {
        foreach (var icon in icons)
        {
            if (icon.Bounds.Contains(point))
            {
                return icon.Process;
            }
        }

        return null;
    }

    /// <summary>An existing window is always raised; starting the executable is what opening it means otherwise.</summary>
    public static ProcessActivation Decide(bool hasWindow, bool canLaunch) => (hasWindow, canLaunch) switch
    {
        (true, _) => ProcessActivation.Focus,
        (false, true) => ProcessActivation.Launch,
        _ => ProcessActivation.None
    };
}
