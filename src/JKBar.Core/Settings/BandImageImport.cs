// Rules for the copy of the user's band image that JKBar keeps for itself.
using IoPath = System.IO.Path;

namespace JKBar.Core.Settings;

public static class BandImageImport
{
    /// <summary>Every copy JKBar owns starts with this, so old ones can be found and cleared out.</summary>
    public const string Prefix = "user_image_";

    public const string Extension = ".png";

    /// <summary>
    /// The image is drawn at the height of the band, which is a notch height even on a 200% display. Storing
    /// anything larger only costs disk and decode time.
    /// </summary>
    public const int MaximumHeight = 128;

    /// <summary>A wide banner still has to leave room for the readouts beside it.</summary>
    public const int MaximumWidth = 512;

    /// <summary>Shrinks to fit the limits while keeping the shape. A small image is left as it is.</summary>
    public static (int Width, int Height) Fit(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return (0, 0);
        }

        var scale = Math.Min(1d, Math.Min(MaximumWidth / (double)width, MaximumHeight / (double)height));

        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    /// <summary>Named by the moment it was taken, so a new pick never collides with the file still in use.</summary>
    public static string FileNameFor(DateTimeOffset when) =>
        $"{Prefix}{when.UtcDateTime:yyyyMMddHHmmssfff}{Extension}";

    /// <summary>True when the path already points at a copy JKBar made in its own folder.</summary>
    public static bool IsImportedCopy(string? path, string folder)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(folder))
        {
            return false;
        }

        try
        {
            var file = IoPath.GetFileName(path);
            return file.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
                && IoPath.GetFullPath(IoPath.GetDirectoryName(path) ?? string.Empty)
                    .Equals(IoPath.GetFullPath(folder), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
