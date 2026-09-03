// Ported from JKMon's PlacementMath, reworked for a bar that hangs from the centre of the top edge.
namespace JKBar.Core.Layout;

/// <summary>Where the notch sits, computed in physical pixels so mixed-DPI monitors need no conversion.</summary>
public static class NotchGeometry
{
    public readonly record struct Rect(int Left, int Top, int Right, int Bottom)
    {
        public int Width => Right - Left;

        public int Height => Bottom - Top;
    }

    /// <summary>
    /// Centred against the top edge of <paramref name="screen"/>. A real notch is a hole in the bezel, so it hugs
    /// the panel edge rather than the work area, and a top-docked taskbar would sit over it.
    /// </summary>
    public static Rect Place(Rect screen, NotchMetrics metrics)
    {
        var width = Math.Min(metrics.Width, Math.Max(1, screen.Width));
        var height = Math.Min(metrics.Height, Math.Max(1, screen.Height));
        var left = screen.Left + ((screen.Width - width) / 2);

        return new Rect(left, screen.Top, left + width, screen.Top + height);
    }
}
