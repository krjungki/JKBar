// How large the idle pet is drawn, kept out of the renderer so the proportions can be checked without a screen.
namespace JKBar.Core.Presentation;

public static class PixelPetLayout
{
    /// <summary>Room left above the pet for its jump, as a share of the notch height.</summary>
    public const double HeadroomShareOfHeight = 0.17;

    /// <summary>Clearance either side, so the pet never touches the notch's rounded corners.</summary>
    public const int SideMargin = 8;

    /// <summary>
    /// Pixels per sprite dot, deliberately fractional. Whole numbers left up to a third of a tall notch unused
    /// and made the pet the same size across two very different bars, which is why it looked small on a big screen.
    /// </summary>
    public static double Scale(int notchWidth, int notchHeight)
    {
        var headroom = Math.Max(3d, notchHeight * HeadroomShareOfHeight);

        return Math.Max(1d, Math.Min(
            (notchHeight - headroom) / PixelPetSprites.Height,
            (notchWidth - SideMargin) / (double)PixelPetSprites.Width));
    }

    public static bool Fits(int notchWidth, int notchHeight) =>
        notchHeight >= PixelPetSprites.Height + 4 && notchWidth >= PixelPetSprites.Width + SideMargin;
}
