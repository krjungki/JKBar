// Draws cached pixel poses at DPI-scaled dot boundaries without GPU resources.
using System.Drawing.Drawing2D;
using JKBar.Core.Layout;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;

namespace JKBar.App.Rendering;

internal static class PixelPetRenderer
{
    internal static void Paint(Graphics graphics, NotchMetrics metrics, IdleNotchContent pet, TimeSpan elapsed)
    {
        var pose = PixelPetAnimation.At(elapsed);
        var sprite = PixelPetSprites.For(pet, pose);
        if (sprite.Count == 0 || !PixelPetLayout.Fits(metrics.Width, metrics.Height))
        {
            return;
        }

        var scale = PixelPetLayout.Scale(metrics.Width, metrics.Height);
        var pixel = Math.Max(1, (int)Math.Round(scale));
        var side = (int)Math.Round(PixelPetSprites.Height * scale);
        var climbing = pose.Action == PixelPetAction.Climb;
        var padding = Math.Min(Math.Max(4, metrics.BottomCornerRadius + 2), Math.Max(2, (metrics.Width - side) / 2));
        var travel = Math.Max(0, metrics.Width - padding * 2 - side);
        var left = padding + (int)Math.Round(travel * pose.Position);
        var floor = Math.Max(2, metrics.Height - side - 2);
        var top = floor - (int)Math.Round((floor - 2) * pose.Elevation);
        if (climbing)
        {
            var wall = pose.Position < 0.5 ? 2 : metrics.Width - side - 2;
            left += (int)Math.Round((wall - left) * pose.Elevation);
        }
        using var fur = new SolidBrush(pet switch
        {
            IdleNotchContent.Dog => Color.FromArgb(230, 164, 117),
            IdleNotchContent.Cat => Color.FromArgb(244, 202, 150),
            _ => Color.FromArgb(238, 235, 225)
        });
        using var patch = new SolidBrush(pet switch
        {
            IdleNotchContent.Dog => Color.FromArgb(191, 91, 55),
            IdleNotchContent.Cat => Color.FromArgb(194, 116, 61),
            // A panda's black has to stay lighter than the cutout or its ears and arms vanish into it.
            _ => Color.FromArgb(92, 101, 110)
        });
        using var pink = new SolidBrush(Color.FromArgb(244, 147, 164));
        using var highlight = new SolidBrush(Color.FromArgb(255, 252, 244));
        using var outline = new SolidBrush(pet == IdleNotchContent.Panda
            ? Color.FromArgb(74, 82, 92)
            : Color.FromArgb(39, 32, 43));
        using var shadow = new SolidBrush(Color.FromArgb(183, 156, 183));
        using var collar = new SolidBrush(Color.FromArgb(196, 40, 76));
        using var charm = new SolidBrush(Color.FromArgb(255, 184, 105));
        using var nose = new SolidBrush(Color.FromArgb(9, 12, 18));
        var state = graphics.Save();
        try
        {
            using var silhouette = NotchRenderer.Silhouette(metrics.Width, metrics.Height, metrics.BottomCornerRadius);
            graphics.SetClip(silhouette, CombineMode.Intersect);
            graphics.SmoothingMode = SmoothingMode.None;
            graphics.PixelOffsetMode = PixelOffsetMode.None;

            // Only mirrored, never turned: the face has to keep looking at the user while the pet moves sideways.
            void Dot(int column, int row, Brush brush)
            {
                var horizontal = pose.FacingLeft ? PixelPetSprites.Width - 1 - column : column;
                var vertical = row;

                // Edges are rounded rather than the size, so neighbouring dots still tile at a fractional scale.
                var x = left + (int)Math.Round(horizontal * scale);
                var y = top + (int)Math.Round(vertical * scale);
                var edgeRight = left + (int)Math.Round((horizontal + 1) * scale);
                var edgeBottom = top + (int)Math.Round((vertical + 1) * scale);
                if (edgeRight > x && edgeBottom > y)
                    graphics.FillRectangle(brush, x, y, edgeRight - x, edgeBottom - y);
            }

            for (var row = 0; row < sprite.Count; row++)
            {
                for (var column = 0; column < sprite[row].Length; column++)
                {
                    Brush? brush = sprite[row][column] switch
                    {
                        'f' => fur,
                        'b' => patch,
                        'p' => pink,
                        'h' => highlight,
                        'o' => outline,
                        's' => shadow,
                        'r' => collar,
                        'c' => charm,
                        'n' => nose,
                        _ => null
                    };
                    if (brush is null) continue;
                    Dot(column, row, brush);
                }
            }

            if (pose.Action == PixelPetAction.Speak)
            {
                DrawBubble(graphics, metrics, new Rectangle(left, top, side, side), PixelPetAnimation.Message(pet, pose.Message), pixel);
            }
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    private static void DrawBubble(Graphics graphics, NotchMetrics metrics, Rectangle pet, string message, int pixel)
    {
        var margin = Math.Max(3, pixel * 2);
        var gap = pixel * 3;
        var rightSpace = metrics.Width - margin - pet.Right - gap;
        var leftSpace = pet.Left - gap - margin;
        var onRight = rightSpace >= leftSpace;
        var available = onRight ? rightSpace : leftSpace;
        if (available < 24) return;

        using var font = new Font("Segoe UI", Math.Clamp(metrics.Height * 0.26f, 8f, 14f), FontStyle.Regular, GraphicsUnit.Pixel);
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter
        };
        var measured = graphics.MeasureString(message, font, PointF.Empty, format);
        var width = Math.Min(available, (int)Math.Ceiling(measured.Width) + margin * 2);
        var height = Math.Min(metrics.Height - margin * 2, (int)Math.Ceiling(font.GetHeight(graphics)) + pixel * 4);
        var left = onRight ? pet.Right + gap : pet.Left - gap - width;
        var top = margin;
        using var paper = new SolidBrush(Color.FromArgb(250, 248, 241));
        using var ink = new SolidBrush(Color.FromArgb(31, 33, 38));
        graphics.FillRectangle(paper, left + pixel, top, width - pixel * 2, height);
        graphics.FillRectangle(paper, left, top + pixel, width, height - pixel * 2);
        var tailLeft = onRight ? left - pixel * 2 : left + width;
        graphics.FillRectangle(paper, tailLeft, top + height - pixel * 4, pixel * 2, pixel * 2);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.DrawString(message, font, ink, new RectangleF(left + margin, top, width - margin * 2, height), format);
    }
}