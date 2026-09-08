// Keeps JKBar's own copy of the band image, so the picture survives the user moving or deleting the original.
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using JKBar.Core.Settings;

namespace JKBar.App;

[SupportedOSPlatform("windows")]
internal static class BandImageStore
{
    /// <summary>Returns the path of the copy, or null when the file could not be read as a picture.</summary>
    internal static string? Import(string sourcePath, string folder)
    {
        using var source = BandImage.Load(sourcePath);
        if (source is null || string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        var (width, height) = BandImageImport.Fit(source.Width, source.Height);
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var target = System.IO.Path.Combine(folder, BandImageImport.FileNameFor(DateTimeOffset.Now));
        try
        {
            Directory.CreateDirectory(folder);
            using (var resized = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(resized))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.Clear(Color.Transparent);
                    graphics.DrawImage(source, new Rectangle(0, 0, width, height));
                }

                // PNG keeps the transparency a logo usually has, whatever the original format was.
                resized.Save(target, ImageFormat.Png);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or System.Runtime.InteropServices.ExternalException)
        {
            return null;
        }

        RemoveOthers(folder, target);
        return target;
    }

    /// <summary>Only the copy in use is kept; every earlier one is rubbish the moment a new picture is chosen.</summary>
    private static void RemoveOthers(string folder, string keep)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, $"{BandImageImport.Prefix}*{BandImageImport.Extension}"))
            {
                if (!file.Equals(keep, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            // A leftover copy is only wasted disk space, never a reason to refuse the new picture.
        }
    }
}
