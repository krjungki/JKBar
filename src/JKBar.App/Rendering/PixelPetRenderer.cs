// Draws crisp integer-sized pixels without textures, icon fonts or GPU resources.
using System.Drawing.Drawing2D;
using JKBar.Core.Layout;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;

namespace JKBar.App.Rendering;

internal static class PixelPetRenderer
{
    internal static void Paint(Graphics graphics, NotchMetrics metrics, IdleNotchContent pet, TimeSpan elapsed)
    {
        var sprite = PixelPetSprites.For(pet);
        if (sprite.Count == 0 || !PixelPetLayout.Fits(metrics.Width, metrics.Height))
        {
            return;
        }

        var pose = PixelPetAnimation.At(elapsed);
        var scale = PixelPetLayout.Scale(metrics.Width, metrics.Height);
        var pixel = Math.Max(1, (int)Math.Round(scale));
        var side = (int)Math.Round(PixelPetSprites.Height * scale);
        var climbing = pose.Action == PixelPetAction.Climb;
        var upright = climbing && pose.Elevation > 0.15;
        var padding = Math.Max(4, metrics.BottomCornerRadius + 2);
        var travel = Math.Max(0, metrics.Width - padding * 2 - side);
        var left = padding + (int)Math.Round(travel * pose.Position);
        var floor = Math.Max(2, metrics.Height - side - 2);
        var top = floor - (int)Math.Round((floor - 2) * pose.Elevation);
        if (climbing)
        {
            var wall = pose.Position < 0.5 ? 2 : metrics.Width - side - 2;
            left += (int)Math.Round((wall - left) * pose.Elevation);
        }
        var bounce = pose.Action == PixelPetAction.Walk && pose.Step % 2 == 1 ? -1 : 0;
        using var fur = new SolidBrush(pet switch
        {
            IdleNotchContent.Dog => Color.FromArgb(244, 212, 159),
            IdleNotchContent.Cat => Color.FromArgb(223, 228, 239),
            _ => Color.FromArgb(247, 247, 240)
        });
        using var patch = new SolidBrush(pet switch
        {
            IdleNotchContent.Dog => Color.FromArgb(168, 111, 70),
            IdleNotchContent.Cat => Color.FromArgb(137, 153, 184),
            _ => Color.FromArgb(65, 69, 80)
        });
        using var pink = new SolidBrush(Color.FromArgb(244, 147, 164));
        using var eyes = new SolidBrush(pet == IdleNotchContent.Panda ? Color.White : Color.FromArgb(36, 32, 40));
        using var nose = new SolidBrush(Color.FromArgb(36, 32, 40));
        var state = graphics.Save();
        try
        {
            using var silhouette = NotchRenderer.Silhouette(metrics.Width, metrics.Height, metrics.BottomCornerRadius);
            graphics.SetClip(silhouette, CombineMode.Intersect);
            graphics.SmoothingMode = SmoothingMode.None;
            graphics.PixelOffsetMode = PixelOffsetMode.None;

            void Dot(int column, int row, Brush brush)
            {
                var horizontal = (pose.FacingLeft ? PixelPetSprites.Width - 1 - column : column) + 1;
                var vertical = row + bounce + 1;
                if (upright)
                {
                    (horizontal, vertical) = pose.FacingLeft
                        ? (PixelPetSprites.Height - 1 - vertical, horizontal)
                        : (vertical, PixelPetSprites.Height - 1 - horizontal);
                }

                // Edges are rounded rather than the size, so neighbouring dots still tile at a fractional scale.
                var x = left + (int)Math.Round(horizontal * scale);
                var y = top + (int)Math.Round(vertical * scale);
                var edgeRight = left + (int)Math.Round((horizontal + 1) * scale);
                var edgeBottom = top + (int)Math.Round((vertical + 1) * scale);
                graphics.FillRectangle(brush, x, y, Math.Max(1, edgeRight - x), Math.Max(1, edgeBottom - y));
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
                        'e' => eyes,
                        'n' => nose,
                        _ => null
                    };
                    if (brush is null) continue;
                    Dot(column, row, brush);
                    if (sprite[row][column] == 'e' && pose.Blink) Dot(column - 1, row, brush);
                }
            }

            var stride = pose.Resting || pose.Action == PixelPetAction.Jump ? 0 : pose.Step < 2 ? 1 : -1;
            for (var foot = 0; foot < 2; foot++)
            {
                var column = 4 + foot * 5;
                Dot(column, 14, patch);
                Dot(column + (foot == 0 ? stride : -stride), 15, patch);
                Dot(column + (foot == 0 ? stride : -stride) + 1, 15, patch);
            }

            var tail = pose.Step < 2 ? 0 : 1;
            Dot(1, 9, patch);
            Dot(0, 8 - tail, patch);
            if (pet == IdleNotchContent.Cat)
            {
                Dot(0, 7 - tail, patch);
                Dot(1, 6 - tail, patch);
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