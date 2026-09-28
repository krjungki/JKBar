// sRGB colour arithmetic for judging text contrast the way WCAG 2 defines it.
using System.Drawing;

namespace JKBar.Core.Layout;

public static class ColourContrast
{
    /// <summary>The Rec. 709 weights apply to linear light, so each sRGB channel is decoded first.</summary>
    public static double RelativeLuminance(Color colour) =>
        0.2126 * Linear(colour.R) + 0.7152 * Linear(colour.G) + 0.0722 * Linear(colour.B);

    public static double Ratio(double first, double second) =>
        (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);

    /// <summary>
    /// Straight sRGB interpolation, which is also how the window manager composites a layered window over the desktop.
    /// </summary>
    public static Color Mix(Color over, Color under, double alpha)
    {
        alpha = Math.Clamp(alpha, 0d, 1d);
        return Color.FromArgb(
            Channel(over.R, under.R, alpha),
            Channel(over.G, under.G, alpha),
            Channel(over.B, under.B, alpha));
    }

    /// <summary>The neutral grey with the given relative luminance.</summary>
    public static Color GreyOf(double luminance)
    {
        var value = (int)Math.Round(Encode(Math.Clamp(luminance, 0d, 1d)) * 255d);
        return Color.FromArgb(value, value, value);
    }

    public static double Linear(byte channel)
    {
        var value = channel / 255d;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static double Encode(double linear) =>
        linear <= 0.0031308 ? linear * 12.92 : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055;

    private static int Channel(byte over, byte under, double alpha) =>
        (int)Math.Round(over * alpha + under * (1 - alpha));
}
