// Small vector icons for the band's news and stock slots, drawn with GDI+ shapes so they stay crisp at any size.
using System.Drawing.Drawing2D;

namespace JKBar.App.Rendering;

internal static class BandIcons
{
    private static readonly Color Outline = Color.FromArgb(36, 36, 40);
    private static readonly Color Paper = Color.FromArgb(250, 250, 252);
    private static readonly Color PaperBack = Color.FromArgb(222, 224, 236);
    private static readonly Color Accent = Color.FromArgb(255, 128, 146);
    private static readonly Color MicHead = Color.FromArgb(132, 140, 176);
    private static readonly Color MicBand = Color.FromArgb(255, 206, 92);
    private static readonly Color MicHandle = Color.FromArgb(184, 188, 206);
    private static readonly Color ChartLine = Color.FromArgb(68, 96, 122);
    private static readonly Color[] Bars =
    [
        Color.FromArgb(147, 193, 63),
        Color.FromArgb(99, 174, 225),
        Color.FromArgb(236, 102, 96),
        Color.FromArgb(255, 185, 49)
    ];

    /// <summary>A newspaper page with a microphone in front of it: a headline bar, two lines and a highlight.</summary>
    internal static void DrawNews(Graphics g, RectangleF area)
    {
        var s = Math.Min(area.Width, area.Height);
        var x = area.Left + ((area.Width - s) / 2f);
        var y = area.Top + ((area.Height - s) / 2f);
        RectangleF At(float left, float top, float right, float bottom) =>
            RectangleF.FromLTRB(x + (left * s), y + (top * s), x + (right * s), y + (bottom * s));

        var stroke = Math.Max(1f, s * 0.07f);
        using var outline = new Pen(Outline, stroke) { LineJoin = LineJoin.Round };
        using var lines = new Pen(Outline, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round };

        FillOutlined(g, PaperBack, outline, At(0.04f, 0.24f, 0.30f, 0.90f), s * 0.06f);
        FillOutlined(g, Paper, outline, At(0.14f, 0.04f, 0.78f, 0.90f), s * 0.07f);

        using (var headline = new SolidBrush(Outline))
        {
            g.FillRectangle(headline, At(0.24f, 0.14f, 0.66f, 0.28f));
        }

        g.DrawLine(lines, x + (0.26f * s), y + (0.42f * s), x + (0.60f * s), y + (0.42f * s));
        g.DrawLine(lines, x + (0.26f * s), y + (0.56f * s), x + (0.52f * s), y + (0.56f * s));
        using (var accent = new SolidBrush(Accent))
        {
            g.FillRectangle(accent, At(0.26f, 0.68f, 0.52f, 0.78f));
        }

        var head = At(0.62f, 0.36f, 0.94f, 0.66f);
        using (var fill = new SolidBrush(MicHead))
        {
            g.FillEllipse(fill, head);
        }

        g.DrawEllipse(outline, head);

        using var handle = new GraphicsPath();
        handle.AddPolygon(
        [
            new PointF(x + (0.68f * s), y + (0.70f * s)),
            new PointF(x + (0.88f * s), y + (0.70f * s)),
            new PointF(x + (0.84f * s), y + (0.97f * s)),
            new PointF(x + (0.72f * s), y + (0.97f * s))
        ]);
        using (var fill = new SolidBrush(MicHandle))
        {
            g.FillPath(fill, handle);
        }

        g.DrawPath(outline, handle);
        FillOutlined(g, MicBand, outline, At(0.60f, 0.60f, 0.96f, 0.74f), s * 0.03f);
    }

    /// <summary>Four coloured bars under a trend line with round markers, the usual shorthand for a price chart.</summary>
    internal static void DrawStock(Graphics g, RectangleF area)
    {
        var s = Math.Min(area.Width, area.Height);
        var x = area.Left + ((area.Width - s) / 2f);
        var y = area.Top + ((area.Height - s) / 2f);

        float[] heights = [0.34f, 0.46f, 0.28f, 0.38f];
        var barWidth = s * 0.16f;
        for (var index = 0; index < heights.Length; index++)
        {
            var left = x + (s * (0.08f + (index * 0.235f)));
            using var fill = new SolidBrush(Bars[index]);
            g.FillRectangle(fill, left, y + (s * (0.96f - heights[index])), barWidth, s * heights[index]);
        }

        PointF[] trend =
        [
            new(x + (s * 0.16f), y + (s * 0.40f)),
            new(x + (s * 0.40f), y + (s * 0.18f)),
            new(x + (s * 0.63f), y + (s * 0.36f)),
            new(x + (s * 0.87f), y + (s * 0.12f))
        ];
        using var line = new Pen(ChartLine, Math.Max(1f, s * 0.07f)) { LineJoin = LineJoin.Round };
        g.DrawLines(line, trend);

        var dot = Math.Max(2f, s * 0.13f);
        using var marker = new SolidBrush(ChartLine);
        foreach (var point in trend)
        {
            g.FillEllipse(marker, point.X - (dot / 2f), point.Y - (dot / 2f), dot, dot);
        }
    }

    private static void FillOutlined(Graphics g, Color fill, Pen outline, RectangleF bounds, float radius)
    {
        using var path = new GraphicsPath();
        var diameter = Math.Max(0.5f, radius * 2f);
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        using var brush = new SolidBrush(fill);
        g.FillPath(brush, path);
        g.DrawPath(outline, path);
    }
}
