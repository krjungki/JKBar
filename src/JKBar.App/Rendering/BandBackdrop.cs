// Reproduces the strip of wallpaper under the band at a quarter of its size, measures it, and frosts it on request.
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using JKBar.App.Interop;
using JKBar.Core.Layout;

namespace JKBar.App.Rendering;

internal static class BandBackdrop
{
    private const int Downscale = 4;
    private const double Saturation = 1.18;

    /// <param name="band">Screen rectangle of the band.</param>
    /// <returns>The frosted backdrop at band size when <paramref name="blur"/> is set, and the measurement of what
    /// the band actually sits on.</returns>
    internal static (Bitmap? Backdrop, BackdropAnalysis Analysis) Build(WallpaperSource source, Rectangle band, bool blur)
    {
        // The blur reaches below the band too, so the wallpaper is sampled twice as tall as the band is drawn.
        var sampleHeight = band.Height * 2;
        var width = Math.Max(1, (band.Width + Downscale - 1) / Downscale);
        var height = Math.Max(2, (sampleHeight + Downscale - 1) / Downscale);
        var bandRows = Math.Clamp((band.Height + Downscale - 1) / Downscale, 1, height);

        using var small = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(small))
        {
            g.Clear(Color.FromArgb(255, source.Background));
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.ScaleTransform(1f / Downscale, 1f / Downscale);
            g.TranslateTransform(-band.Left, -band.Top);
            DrawWallpaper(g, source, new Rectangle(band.Left, band.Top, band.Width, sampleHeight));
        }

        var pixels = ReadPixels(small);
        if (blur)
        {
            BackdropAnalysis.Saturate(pixels, Saturation);
            BackdropAnalysis.Blur(pixels, width, height, Math.Max(2, bandRows / 2));
        }

        var analysis = BackdropAnalysis.Analyze(pixels.AsSpan(0, width * bandRows * 4), width, bandRows);
        return (blur ? Upscale(small, pixels, band.Size) : null, analysis);
    }

    private static void DrawWallpaper(Graphics g, WallpaperSource source, Rectangle area)
    {
        foreach (var path in Candidates(source.ImagePath))
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
                var destination = WallpaperPlacement.Destination(image.Size, source.Monitor, source.VirtualDesktop, source.Fit);

                if (source.Fit == WallpaperFit.Tile)
                {
                    using var tiles = new TextureBrush(image, WrapMode.Tile);
                    tiles.TranslateTransform(destination.Left, destination.Top);
                    g.FillRectangle(tiles, area);
                }
                else
                {
                    using var edges = new ImageAttributes();
                    edges.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(
                        image,
                        [destination.Location, new PointF(destination.Right, destination.Top), new PointF(destination.Left, destination.Bottom)],
                        new RectangleF(0, 0, image.Width, image.Height),
                        GraphicsUnit.Pixel,
                        edges);
                }

                return;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
                or OutOfMemoryException or ExternalException)
            {
                // GDI+ reports an undecodable file as out of memory; the next candidate or the fill colour stands in.
            }
        }
    }

    /// <summary>The configured file first; Windows' own re-encoded copy only when that file cannot be opened.</summary>
    private static IEnumerable<string> Candidates(string? configured)
    {
        if (configured is null)
        {
            yield break;
        }

        if (File.Exists(configured))
        {
            yield return configured;
        }

        if (File.Exists(DesktopWallpaperInterop.TranscodedPath))
        {
            yield return DesktopWallpaperInterop.TranscodedPath;
        }
    }

    private static byte[] ReadPixels(Bitmap bitmap)
    {
        var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var row = bitmap.Width * 4;
            var pixels = new byte[row * bitmap.Height];
            for (var y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * row, row);
            }

            return pixels;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static void WritePixels(Bitmap bitmap, byte[] pixels)
    {
        var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            var row = bitmap.Width * 4;
            for (var y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(pixels, y * row, data.Scan0 + y * data.Stride, row);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static Bitmap Upscale(Bitmap small, byte[] pixels, Size size)
    {
        WritePixels(small, pixels);

        var backdrop = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(backdrop))
        using (var edges = new ImageAttributes())
        {
            edges.SetWrapMode(WrapMode.TileFlipXY);
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(
                small,
                new Rectangle(Point.Empty, size),
                0f,
                0f,
                size.Width / (float)Downscale,
                size.Height / (float)Downscale,
                GraphicsUnit.Pixel,
                edges);
        }

        // Resampling can leave a fringe of partial alpha, and the band relies on this layer being solid.
        var opaque = ReadPixels(backdrop);
        for (var index = 3; index < opaque.Length; index += 4)
        {
            opaque[index] = 255;
        }

        WritePixels(backdrop, opaque);
        return backdrop;
    }
}
