// Paints the reserved band: the fill, the user's image on the left, and the readouts on the right.
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using JKBar.Core.Layout;
using JKBar.Core.Presentation;

namespace JKBar.App.Rendering;

internal static class BandRenderer
{
    private const float FontShareOfHeight = 0.375f;
    private const float PaddingShareOfHeight = 0.22f;

    /// <param name="notch">Where the bar sits, in surface coordinates, so content can keep clear of it.</param>
    /// <param name="items">In display order, left to right.</param>
    internal static void Paint(
        Graphics g,
        Size surface,
        NotchGeometry.Rect notch,
        BandStyle style,
        Image? image,
        IReadOnlyList<BandItem> items)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        // ClearType leaves black fringes on a layered surface because it assumes an opaque background.
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.Clear(style.ForLayeredSurface());

        var padding = (int)Math.Round(surface.Height * PaddingShareOfHeight);
        var band = new NotchGeometry.Rect(0, 0, surface.Width, surface.Height);
        var slots = BandLayout.Divide(band, notch, padding);

        DrawImage(g, slots.Left, image, padding);

        using var font = new Font("Segoe UI", surface.Height * FontShareOfHeight, GraphicsUnit.Pixel);
        DrawItems(g, slots.Right, style, items, font, padding);
    }

    private static void DrawImage(Graphics g, NotchGeometry.Rect slot, Image? image, int padding)
    {
        if (image is null || slot.Width <= 0)
        {
            return;
        }

        var available = slot.Height - padding;
        if (available <= 0)
        {
            return;
        }

        var factor = Math.Min(available / (double)image.Height, slot.Width / (double)image.Width);
        var width = (int)Math.Round(image.Width * factor);
        var height = (int)Math.Round(image.Height * factor);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        g.DrawImage(image, slot.Left, slot.Top + ((slot.Height - height) / 2), width, height);
    }

    private static void DrawItems(
        Graphics g, NotchGeometry.Rect slot, BandStyle style, IReadOnlyList<BandItem> items, Font font, int padding)
    {
        if (slot.Width <= 0 || items.Count == 0)
        {
            return;
        }

        using var format = StringFormat.GenericTypographic;
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

        using var text = new SolidBrush(style.TextColour);
        using var shadow = new SolidBrush(style.ShadowColour);
        using var muted = new SolidBrush(Color.FromArgb(170, style.TextColour));

        var gap = padding;
        var right = slot.Right;

        // Right-aligned, so the list is walked backwards and callers can supply plain left-to-right order.
        for (var i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            var value = item.Value;
            var label = item.Label;
            var valueWidth = Measure(g, value, font, format);
            var labelWidth = string.IsNullOrEmpty(label) ? 0 : Measure(g, label, font, format) + (gap / 2);

            var start = right - valueWidth - labelWidth;
            if (start < slot.Left)
            {
                return;
            }

            var y = slot.Top + ((slot.Height - font.Height) / 2f);

            if (labelWidth > 0)
            {
                Write(g, label, font, format, muted, shadow, start, y);
            }

            using var accent = item.Accent is { } colour ? new SolidBrush(colour) : null;
            Write(g, value, font, format, accent ?? text, shadow, start + labelWidth, y);

            right = start - gap;
        }
    }

    private static void Write(
        Graphics g, string s, Font font, StringFormat format, Brush brush, Brush shadow, float x, float y)
    {
        g.DrawString(s, font, shadow, x + 1, y + 1, format);
        g.DrawString(s, font, brush, x, y, format);
    }

    private static int Measure(Graphics g, string s, Font font, StringFormat format) =>
        (int)Math.Ceiling(g.MeasureString(s, font, PointF.Empty, format).Width);
}
