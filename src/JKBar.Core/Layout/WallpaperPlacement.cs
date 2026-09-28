// Where Windows draws the wallpaper image for each of its fit choices, so a strip of it can be reproduced exactly.
using System.Drawing;

namespace JKBar.Core.Layout;

/// <summary>Same numbering as DESKTOP_WALLPAPER_POSITION.</summary>
public enum WallpaperFit
{
    Center = 0,
    Tile = 1,
    Stretch = 2,
    Fit = 3,
    Fill = 4,
    Span = 5
}

public static class WallpaperPlacement
{
    /// <summary>
    /// The image's rectangle in screen coordinates. For <see cref="WallpaperFit.Tile"/> it is the first tile, anchored
    /// at the monitor's top left; Span fills the whole virtual desktop instead of one monitor.
    /// </summary>
    public static RectangleF Destination(SizeF image, RectangleF monitor, RectangleF virtualDesktop, WallpaperFit fit)
    {
        if (image.Width <= 0 || image.Height <= 0)
        {
            return RectangleF.Empty;
        }

        return fit switch
        {
            WallpaperFit.Center => Centered(image, monitor, 1f),
            WallpaperFit.Tile => new RectangleF(monitor.Location, image),
            WallpaperFit.Stretch => monitor,
            WallpaperFit.Fit => Centered(image, monitor, Math.Min(monitor.Width / image.Width, monitor.Height / image.Height)),
            WallpaperFit.Span => Centered(image, virtualDesktop,
                Math.Max(virtualDesktop.Width / image.Width, virtualDesktop.Height / image.Height)),
            _ => Centered(image, monitor, Math.Max(monitor.Width / image.Width, monitor.Height / image.Height))
        };
    }

    private static RectangleF Centered(SizeF image, RectangleF area, float scale)
    {
        var width = image.Width * scale;
        var height = image.Height * scale;
        return new RectangleF(
            area.Left + (area.Width - width) / 2f,
            area.Top + (area.Height - height) / 2f,
            width,
            height);
    }
}
