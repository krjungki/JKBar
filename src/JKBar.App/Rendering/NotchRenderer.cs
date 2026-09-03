// Draws the notch silhouette: square where it meets the panel edge, rounded where it hangs into the screen.
using System.Drawing.Drawing2D;
using JKBar.Core.Layout;

namespace JKBar.App.Rendering;

internal static class NotchRenderer
{
    /// <summary>
    /// Apple gives the cutout a smaller radius at the top than the bottom. Ours starts flush against the panel
    /// edge, so only the bottom pair is drawn as a curve and the top corners stay square.
    /// </summary>
    internal static void Paint(Graphics g, NotchMetrics metrics, Color fill)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
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
            path.AddRectangle(new Rectangle(0, 0, width, height));
            return path;
        }

        // The bitmap is exactly the notch, so the right and bottom edges land on width-1 and height-1.
        var right = width - 1;
        var bottom = height - 1;
        var diameter = radius * 2;

        path.StartFigure();
        path.AddLine(0, 0, right, 0);
        path.AddLine(right, 0, right, bottom - radius);
        path.AddArc(right - diameter, bottom - diameter, diameter, diameter, 0, 90);
        path.AddLine(radius, bottom, radius, bottom);
        path.AddArc(0, bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }
}
