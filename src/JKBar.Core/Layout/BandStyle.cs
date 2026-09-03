// Fill for the reserved band either side of the notch.
using System.Drawing;

namespace JKBar.Core.Layout;

public sealed record BandStyle(Color Colour, int OpacityPercent)
{
    public static BandStyle Default { get; } = new(Color.White, 25);

    public int Opacity => Math.Clamp(OpacityPercent, 0, 100);

    /// <summary>
    /// Alpha and colour ready for UpdateLayeredWindow, which reads its source as premultiplied. Handing it
    /// straight alpha leaves a half-transparent fill looking far brighter than the colour that was chosen.
    /// </summary>
    public Color ForLayeredSurface()
    {
        var alpha = (int)Math.Round(Opacity * 255 / 100d);

        return Color.FromArgb(alpha, Premultiply(Colour.R, alpha), Premultiply(Colour.G, alpha), Premultiply(Colour.B, alpha));
    }

    private static int Premultiply(byte channel, int alpha) => channel * alpha / 255;
}
