// Draws the notch silhouette: square where it meets the panel edge, rounded where it hangs into the screen.
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using JKBar.Core.Layout;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;

namespace JKBar.App.Rendering;

internal static class NotchRenderer
{
    /// <summary>Drawn above the bitmap so the row against the panel edge can never be a blend of shape and nothing.</summary>
    private const int TopOverdraw = 2;

    /// <summary>
    /// Apple gives the cutout a smaller radius at the top than the bottom. Ours starts flush against the panel
    /// edge, so only the bottom pair is drawn as a curve and the top corners stay square.
    /// </summary>
    internal static void Paint(
        Graphics g,
        NotchMetrics metrics,
        Color fill,
        NotchContent? content,
        BandTypographySettings typography,
        float textSize,
        Image? icon = null,
        NotchGlyph glyph = NotchGlyph.None)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // GDI+ otherwise puts pixel centres on integer coordinates, so a fill starting at 0 covers the first
        // row and column only half way and the desktop shows through the edges.
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.Clear(Color.Transparent);

        using var path = Silhouette(metrics.Width, metrics.Height, metrics.BottomCornerRadius);
        using var brush = new SolidBrush(fill);
        g.FillPath(brush, path);

        if (content is not null)
        {
            PaintContent(g, metrics, content, typography, textSize, icon, glyph);
        }
    }

    private static void PaintContent(
        Graphics g,
        NotchMetrics metrics,
        NotchContent content,
        BandTypographySettings typography,
        float textSize,
        Image? icon,
        NotchGlyph glyph)
    {
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        var style = (typography.Bold ? FontStyle.Bold : FontStyle.Regular)
            | (typography.Italic ? FontStyle.Italic : FontStyle.Regular);

        var size = Math.Max(6f, textSize);

        using var font = CreateFont(typography.FontFamily, size, style);
        using var detailFont = CreateFont(typography.FontFamily, Math.Max(5f, size * 0.72f), style);
        using var foreground = new SolidBrush(Color.FromArgb(242, 242, 247));
        using var muted = new SolidBrush(Color.FromArgb(200, 242, 242, 247));
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };

        var horizontalPadding = Math.Max(4, metrics.Height / 8);
        var width = Math.Max(1, metrics.Width - (horizontalPadding * 2));
        var iconSize = Math.Max(12f, metrics.Height * 0.46f);
        var gap = Math.Max(4f, metrics.Height * 0.12f);

        using var leading = (StringFormat)format.Clone();
        leading.Alignment = StringAlignment.Near;

        // The application's own icon wins; the drawn mark is what stands in when there is none.
        var lead = icon is not null || glyph != NotchGlyph.None;

        void DrawLead(RectangleF box)
        {
            if (icon is not null)
            {
                g.DrawImage(icon, box);
                return;
            }

            NotchGlyphRenderer.Draw(g, glyph, box, foreground.Color);
        }

        if (content.Secondary is null)
        {
            var line = new RectangleF(horizontalPadding, 0, width, metrics.Height);
            if (!lead)
            {
                Write(g, content.Primary, font, foreground, format, typography, line);
                return;
            }

            var textWidth = Fitted(g, [(content.Primary, font)], leading, line, iconSize + gap);
            var left = line.Left + ((line.Width - (iconSize + gap + textWidth)) / 2f);
            DrawLead(new RectangleF(left, line.Top + ((line.Height - iconSize) / 2f), iconSize, iconSize));
            Write(g, content.Primary, font, foreground, leading, typography, new RectangleF(left + iconSize + gap, line.Top, textWidth, line.Height));
            return;
        }

        var primaryHeight = font.GetHeight(g);
        var detailHeight = detailFont.GetHeight(g);
        var top = (metrics.Height - (primaryHeight + detailHeight)) / 2f;

        if (!lead)
        {
            Write(g, content.Primary, font, foreground, format, typography, new RectangleF(horizontalPadding, top, width, primaryHeight));
            Write(g, content.Secondary, detailFont, muted, format, typography, new RectangleF(horizontalPadding, top + primaryHeight, width, detailHeight));
            return;
        }

        var block = new RectangleF(horizontalPadding, top, width, primaryHeight + detailHeight);
        var blockWidth = Fitted(g, [(content.Primary, font), (content.Secondary, detailFont)], leading, block, iconSize + gap);
        var blockLeft = block.Left + ((block.Width - (iconSize + gap + blockWidth)) / 2f);
        DrawLead(new RectangleF(blockLeft, (metrics.Height - iconSize) / 2f, iconSize, iconSize));

        var textLeft = blockLeft + iconSize + gap;
        Write(g, content.Primary, font, foreground, leading, typography, new RectangleF(textLeft, top, blockWidth, primaryHeight));
        Write(g, content.Secondary, detailFont, muted, leading, typography, new RectangleF(textLeft, top + primaryHeight, blockWidth, detailHeight));
    }

    /// <summary>The width the longest line wants, never more than what is left beside the icon.</summary>
    private static float Fitted(
        Graphics g,
        (string Text, Font Font)[] lines,
        StringFormat format,
        RectangleF area,
        float reserved)
    {
        var available = Math.Max(1f, area.Width - reserved);
        var widest = lines.Max(line => g.MeasureString(line.Text, line.Font, new SizeF(available, area.Height), format).Width);

        return Math.Min(available, widest + 2f);
    }

    private static void Write(
        Graphics g,
        string text,
        Font font,
        SolidBrush brush,
        StringFormat format,
        BandTypographySettings typography,
        RectangleF area)
    {
        if (typography.TextShadow)
        {
            using var shadow = new SolidBrush(BandRenderer.ShadowColourFor(brush.Color));
            g.DrawString(text, font, shadow, new RectangleF(area.X + 1, area.Y + 1, area.Width, area.Height), format);
        }

        g.DrawString(text, font, brush, area, format);
    }

    private static Font CreateFont(string family, float size, FontStyle style)
    {
        try
        {
            return new Font(family, size, style, GraphicsUnit.Pixel);
        }
        catch (ArgumentException)
        {
            return new Font(BandTypographySettings.DefaultFontFamily, size, style, GraphicsUnit.Pixel);
        }
    }

    /// <summary>Also used by the band, which has to stamp the same shape so the cutout stays black.</summary>
    internal static GraphicsPath Silhouette(int width, int height, int radius)
    {
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
