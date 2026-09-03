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

    /// <summary>
    /// Text colour for this band. It can only read the chosen colour, not the wallpaper showing through a
    /// translucent one, so the renderer also draws a contrasting shadow rather than trusting this alone.
    /// </summary>
    public Color TextColour => IsLight ? Color.FromArgb(24, 24, 27) : Color.FromArgb(242, 242, 247);

    public Color ShadowColour => IsLight ? Color.FromArgb(90, 255, 255, 255) : Color.FromArgb(110, 0, 0, 0);

    private bool IsLight => ((0.2126 * Colour.R) + (0.7152 * Colour.G) + (0.0722 * Colour.B)) > 140;
}
