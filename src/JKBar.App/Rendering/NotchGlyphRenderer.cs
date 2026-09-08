// Simple single-colour marks for the expanded notch, drawn as paths so no icon font has to be present.
using System.Drawing.Drawing2D;
using JKBar.Core.Alerts;

namespace JKBar.App.Rendering;

internal enum NotchGlyph
{
    None,
    Volume,
    Media,
    Power,
    Network,
    Offline,
    System,
    Device,
    Sync,
    JkBar
}

internal static class NotchGlyphRenderer
{
    /// <summary>An alert that carries a real application icon shows that instead, so Media has no mark of its own.</summary>
    internal static NotchGlyph For(AlertCategory category, AlertSeverity severity) => category switch
    {
        AlertCategory.Audio => NotchGlyph.Volume,
        AlertCategory.Media => NotchGlyph.Media,
        AlertCategory.Power => NotchGlyph.Power,
        AlertCategory.Network => severity == AlertSeverity.Warning ? NotchGlyph.Offline : NotchGlyph.Network,
        AlertCategory.System => NotchGlyph.System,
        AlertCategory.Device => NotchGlyph.Device,
        AlertCategory.Sync => NotchGlyph.Sync,
        _ => NotchGlyph.JkBar
    };

    internal static void Draw(Graphics g, NotchGlyph glyph, RectangleF box, Color ink)
    {
        if (glyph == NotchGlyph.None || box.Width <= 0 || box.Height <= 0)
        {
            return;
        }

        // Everything below is expressed against a square, so one stroke width keeps the set visually consistent.
        var side = Math.Min(box.Width, box.Height);
        var origin = new PointF(box.X + ((box.Width - side) / 2f), box.Y + ((box.Height - side) / 2f));
        var stroke = Math.Max(1.4f, side * 0.09f);

        using var pen = new Pen(ink, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(ink);

        var state = g.Save();
        g.TranslateTransform(origin.X, origin.Y);
        g.ScaleTransform(side, side);
        // The pen is in unit space now, so its width has to be undone by the same factor.
        pen.Width = stroke / side;

        switch (glyph)
        {
            case NotchGlyph.Volume:
                Volume(g, pen, brush);
                break;
            case NotchGlyph.Media:
                Media(g, brush);
                break;
            case NotchGlyph.Power:
                Power(g, brush);
                break;
            case NotchGlyph.Network:
                Globe(g, pen, slashed: false);
                break;
            case NotchGlyph.Offline:
                Globe(g, pen, slashed: true);
                break;
            case NotchGlyph.System:
                Bars(g, brush);
                break;
            case NotchGlyph.Device:
                Device(g, pen, brush);
                break;
            case NotchGlyph.Sync:
                Refresh(g, pen, brush);
                break;
            default:
                Notch(g, brush);
                break;
        }

        g.Restore(state);
    }

    /// <summary>A speaker cone with one sound arc.</summary>
    private static void Volume(Graphics g, Pen pen, Brush brush)
    {
        using var cone = new GraphicsPath();
        cone.AddPolygon(new[]
        {
            new PointF(0.10f, 0.36f),
            new PointF(0.26f, 0.36f),
            new PointF(0.46f, 0.16f),
            new PointF(0.46f, 0.84f),
            new PointF(0.26f, 0.64f),
            new PointF(0.10f, 0.64f)
        });
        g.FillPath(brush, cone);
        g.DrawArc(pen, 0.42f, 0.28f, 0.30f, 0.44f, -60f, 120f);
        g.DrawArc(pen, 0.42f, 0.14f, 0.48f, 0.72f, -60f, 120f);
    }

    private static void Media(Graphics g, Brush brush)
    {
        g.FillPolygon(brush, new[] { new PointF(0.26f, 0.14f), new PointF(0.82f, 0.50f), new PointF(0.26f, 0.86f) });
    }

    private static void Power(Graphics g, Brush brush)
    {
        g.FillPolygon(brush, new[]
        {
            new PointF(0.56f, 0.06f),
            new PointF(0.24f, 0.55f),
            new PointF(0.46f, 0.55f),
            new PointF(0.42f, 0.94f),
            new PointF(0.76f, 0.44f),
            new PointF(0.54f, 0.44f)
        });
    }

    private static void Globe(Graphics g, Pen pen, bool slashed)
    {
        g.DrawEllipse(pen, 0.10f, 0.10f, 0.80f, 0.80f);
        g.DrawEllipse(pen, 0.32f, 0.10f, 0.36f, 0.80f);
        g.DrawLine(pen, 0.12f, 0.50f, 0.88f, 0.50f);

        if (slashed)
        {
            g.DrawLine(pen, 0.14f, 0.86f, 0.86f, 0.14f);
        }
    }

    /// <summary>Three rising bars: the shape people already read as a usage readout.</summary>
    private static void Bars(Graphics g, Brush brush)
    {
        g.FillRectangle(brush, 0.14f, 0.58f, 0.18f, 0.30f);
        g.FillRectangle(brush, 0.41f, 0.38f, 0.18f, 0.50f);
        g.FillRectangle(brush, 0.68f, 0.16f, 0.18f, 0.72f);
    }

    /// <summary>A ring broken by an arrowhead, which is what every product uses for "syncing".</summary>
    private static void Refresh(Graphics g, Pen pen, Brush brush)
    {
        g.DrawArc(pen, 0.16f, 0.16f, 0.68f, 0.68f, 40f, 280f);
        g.FillPolygon(brush, new[]
        {
            new PointF(0.84f, 0.28f),
            new PointF(0.62f, 0.34f),
            new PointF(0.80f, 0.50f)
        });
    }

    /// <summary>A drive: a slab with the activity dot storage enclosures always carry.</summary>
    private static void Device(Graphics g, Pen pen, Brush brush)
    {
        g.DrawRectangle(pen, 0.12f, 0.30f, 0.76f, 0.40f);
        g.FillEllipse(brush, 0.68f, 0.44f, 0.12f, 0.12f);
        g.DrawLine(pen, 0.24f, 0.50f, 0.48f, 0.50f);
    }

    /// <summary>The bar's own silhouette, for anything JKBar says about itself.</summary>
    private static void Notch(Graphics g, Brush brush)
    {
        using var shape = new GraphicsPath();
        shape.AddLine(0.10f, 0.26f, 0.90f, 0.26f);
        shape.AddArc(0.66f, 0.50f, 0.24f, 0.24f, 0f, 90f);
        shape.AddArc(0.10f, 0.50f, 0.24f, 0.24f, 90f, 90f);
        shape.CloseFigure();
        g.FillPath(brush, shape);
    }
}
