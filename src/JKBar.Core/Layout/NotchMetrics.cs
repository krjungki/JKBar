// Notch dimensions live here because the macOS numbers they start from are a judgement call, not a constant of nature.
namespace JKBar.Core.Layout;

/// <summary>
/// Sizes are logical pixels (1/96 inch). Callers multiply by the monitor's DPI scale before placing the window.
/// </summary>
public sealed record NotchMetrics(int Width, int Height, int BottomCornerRadius)
{
    /// <summary>
    /// The 14-inch MacBook Pro cutout is 185 x 32 points with a bottom corner radius near 8, read off NSScreen.
    /// A macOS point is 1/72 inch and a Windows logical pixel is 1/96 inch, so reusing the numbers reproduces
    /// the proportions, not the physical size. Matching real millimetres would need ~247 x 43 instead.
    /// </summary>
    public static NotchMetrics MacBookPro14 { get; } = new(185, 32, 8);

    /// <summary>Apple holds the notch at this share of screen width on every notched model, 14-inch through 16-inch.</summary>
    public const double MacWidthShareOfScreen = 0.122;

    /// <summary>Width that keeps Apple's screen-width ratio on a display of the given physical width.</summary>
    public static int ProportionalWidth(int screenWidthPixels) =>
        Math.Max(1, (int)Math.Round(screenWidthPixels * MacWidthShareOfScreen));

    /// <summary>Converts to physical pixels. The radius can never exceed half the box or the arcs overlap.</summary>
    public NotchMetrics ScaledBy(double factor)
    {
        var width = Math.Max(1, (int)Math.Round(Width * factor));
        var height = Math.Max(1, (int)Math.Round(Height * factor));
        var radius = Math.Max(0, (int)Math.Round(BottomCornerRadius * factor));

        return new NotchMetrics(width, height, Math.Min(radius, Math.Min(width, height) / 2));
    }
}
