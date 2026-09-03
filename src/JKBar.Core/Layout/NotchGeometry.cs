// Ported from JKMon's PlacementMath, reworked for a bar that hangs from the top edge instead of sitting above the taskbar.
namespace JKBar.Core.Layout;

public enum NotchAlignment
{
    Left,
    Centre,
    Right
}

/// <summary>Where the notch sits, computed in physical pixels so mixed-DPI monitors need no conversion.</summary>
public static class NotchGeometry
{
    public readonly record struct Rect(int Left, int Top, int Right, int Bottom)
    {
        public int Width => Right - Left;

        public int Height => Bottom - Top;
    }

    /// <summary>
    /// Anchors to the top edge of <paramref name="screen"/>. A real notch is a hole in the bezel, so it hugs the
    /// panel edge rather than the work area, and a top-docked taskbar would sit over it.
    /// </summary>
    /// <param name="edgeInset">Physical pixels held back from the left or right edge; ignored when centred.</param>
    public static Rect Place(Rect screen, NotchMetrics metrics, NotchAlignment alignment, int edgeInset = 0)
    {
        var width = Math.Min(metrics.Width, Math.Max(1, screen.Width));
        var height = Math.Min(metrics.Height, Math.Max(1, screen.Height));
        var inset = Math.Max(0, Math.Min(edgeInset, screen.Width - width));

        var left = alignment switch
        {
            NotchAlignment.Left => screen.Left + inset,
            NotchAlignment.Right => screen.Right - width - inset,
            _ => screen.Left + (screen.Width - width) / 2
        };

        return new Rect(left, screen.Top, left + width, screen.Top + height);
    }
}
