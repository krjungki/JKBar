// The notch physically occupies the centre of the band, so everything else has to live either side of it.
namespace JKBar.Core.Layout;

public static class BandLayout
{
    public readonly record struct Slots(NotchGeometry.Rect Left, NotchGeometry.Rect Right);

    /// <summary>
    /// Splits the band around the notch. A slot can come back empty when the notch grows wide enough to leave no
    /// room, which callers must treat as "draw nothing" rather than as a negative width.
    /// </summary>
    public static Slots Divide(NotchGeometry.Rect band, NotchGeometry.Rect notch, int padding)
    {
        var left = Between(band.Left + padding, Math.Min(notch.Left, band.Right) - padding, band);
        var right = Between(Math.Max(notch.Right, band.Left) + padding, band.Right - padding, band);

        return new Slots(left, right);
    }

    private static NotchGeometry.Rect Between(int from, int to, NotchGeometry.Rect band)
    {
        var start = Math.Clamp(from, band.Left, band.Right);
        var end = Math.Clamp(to, start, band.Right);

        return new NotchGeometry.Rect(start, band.Top, end, band.Bottom);
    }
}
