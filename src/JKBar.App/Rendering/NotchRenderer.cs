// Draws the notch silhouette: square where it meets the panel edge, rounded where it hangs into the screen.
using System.Drawing.Drawing2D;
using JKBar.Core.Layout;

namespace JKBar.App.Rendering;

internal static class NotchRenderer
{
    /// <summary>Drawn above the bitmap so the row against the panel edge can never be a blend of shape and nothing.</summary>
    private const int TopOverdraw = 2;

    /// <summary>
    /// Apple gives the cutout a smaller radius at the top than the bottom. Ours starts flush against the panel
    /// edge, so only the bottom pair is drawn as a curve and the top corners stay square.
    /// </summary>
    internal static void Paint(Graphics g, NotchMetrics metrics, Color fill)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // GDI+ otherwise puts pixel centres on integer coordinates, so a fill starting at 0 covers the first
        // row and column only half way and the desktop shows through the edges.
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.Clear(Color.Transparent);

        using var path = Silhouette(metrics);
        using var brush = new SolidBrush(fill);
        g.FillPath(brush, path);
    }

    private static GraphicsPath Silhouette(NotchMetrics metrics)
    {
        var width = metrics.Width;
        var height = metrics.Height;
        var radius = metrics.BottomCornerRadius;
        var path = new GraphicsPath();

        if (radius <= 0)
        {
            path.AddRectangle(new Rectangle(0, -TopOverdraw, width, height + TopOverdraw));
            return path;
        }

        var diameter = radius * 2;

        path.StartFigure();
        path.AddLine(0, -TopOverdraw, width, -TopOverdraw);
        path.AddLine(width, -TopOverdraw, width, height - radius);
        path.AddArc(width - diameter, height - diameter, diameter, diameter, 0, 90);
        path.AddArc(0, height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }
}
