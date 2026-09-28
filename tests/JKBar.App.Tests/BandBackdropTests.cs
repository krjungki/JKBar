// Builds a frosted backdrop from a synthetic wallpaper file and paints the band over it, all on the CPU.
using System.Diagnostics;
using System.Drawing.Imaging;
using JKBar.App.Interop;
using JKBar.App.Rendering;
using JKBar.Core.Layout;
using JKBar.Core.Settings;

namespace JKBar.App.Tests;

public sealed class BandBackdropTests
{
    [Fact]
    public void BlurredBackdropIsOpaqueBandSizedAndMeasuresTheTopOfTheWallpaper()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"jkbar-backdrop-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "wallpaper.png");
        try
        {
            // Bright sky over a dark ground: only the sky is under the band.
            using (var wallpaper = new Bitmap(800, 450, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(wallpaper))
            {
                g.Clear(Color.FromArgb(20, 30, 40));
                using var sky = new SolidBrush(Color.FromArgb(230, 235, 245));
                g.FillRectangle(sky, 0, 0, 800, 120);
                wallpaper.Save(file, ImageFormat.Png);
            }

            var monitor = new Rectangle(0, 0, 1600, 900);
            var source = new WallpaperSource(file, WallpaperFit.Fill, Color.Black, monitor, monitor);
            var band = new Rectangle(0, 0, 1600, 40);

            var (backdrop, analysis) = BandBackdrop.Build(source, band, blur: true);
            using (backdrop)
            {
                Assert.NotNull(backdrop);
                Assert.Equal(band.Size, backdrop!.Size);
                Assert.All(new[] { new Point(0, 0), new Point(1599, 39), new Point(800, 20) },
                    point => Assert.Equal(255, backdrop.GetPixel(point.X, point.Y).A));
                Assert.True(analysis.Mean.R > 200, $"mean {analysis.Mean}");
                Assert.Equal(AdaptiveAppearance.DarkText,
                    AdaptiveAppearance.Resolve(analysis, Color.White, 25, false, null).Text);

                using var surface = new Bitmap(1600, 40, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(surface))
                {
                    BandRenderer.Paint(
                        g,
                        surface.Size,
                        new NotchGeometry.Rect(700, 0, 900, 40),
                        12,
                        BandStyle.Default,
                        new BandTypographySettings(),
                        image: null,
                        imageScalePercent: 100,
                        activeApp: "Code",
                        quote: null,
                        news: null,
                        items: [],
                        runningProcesses: [],
                        backdrop: backdrop);
                }

                Assert.Equal(255, surface.GetPixel(100, 20).A);
                Assert.Equal(255, surface.GetPixel(1500, 5).A);
            }

            var (plain, _) = BandBackdrop.Build(source, band, blur: false);
            Assert.Null(plain);

            var forbidden = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
                .Select(module => module.ModuleName ?? string.Empty)
                .Where(name => name.Equals("d2d1.dll", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("d3d11.dll", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("D3D10Warp.dll", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("dxgi.dll", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(forbidden);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void BandAwayFromTheOriginSamplesItsOwnPartOfTheWallpaper()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"jkbar-backdrop-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "halves.png");
        try
        {
            using (var wallpaper = new Bitmap(400, 200, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(wallpaper))
            {
                g.Clear(Color.FromArgb(200, 30, 30));
                using var right = new SolidBrush(Color.FromArgb(30, 30, 200));
                g.FillRectangle(right, 200, 0, 200, 200);
                wallpaper.Save(file, ImageFormat.Png);
            }

            // A second monitor to the right of the first, with the band on its right half.
            var monitor = new Rectangle(1600, 0, 1600, 800);
            var source = new WallpaperSource(file, WallpaperFit.Stretch, Color.Black, monitor, monitor);

            var (_, analysis) = BandBackdrop.Build(source, new Rectangle(2500, 0, 600, 32), blur: false);

            Assert.True(analysis.Mean.B > 180 && analysis.Mean.R < 50, $"mean {analysis.Mean}");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void RegionsGiveEachSideOfTheBandItsOwnInk()
    {
        using var surface = new Bitmap(800, 40, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(surface))
        {
            BandRenderer.Paint(
                g,
                surface.Size,
                new NotchGeometry.Rect(360, 0, 440, 40),
                12,
                BandStyle.Default,
                new BandTypographySettings(),
                image: null,
                imageScalePercent: 100,
                activeApp: "Visual Studio Code",
                quote: null,
                news: null,
                items: [new JKBar.Core.Presentation.BandItem("Mon 28", "19:07", "00:00") { Kind = JKBar.Core.Presentation.BandItemKind.Clock }],
                runningProcesses: [],
                regions: [new RegionLook(AdaptiveAppearance.DarkText, false), new RegionLook(AdaptiveAppearance.LightText, false)]);
        }

        var (leftDark, leftLight) = InkPixels(surface, 0, 360);
        var (rightDark, rightLight) = InkPixels(surface, 440, 800);
        Assert.True(leftDark > 20 && leftLight == 0, $"left dark {leftDark}, light {leftLight}");
        Assert.True(rightLight > 20 && rightDark == 0, $"right dark {rightDark}, light {rightLight}");
    }

    private static (int Dark, int Light) InkPixels(Bitmap surface, int from, int to)
    {
        int dark = 0, light = 0;
        for (var x = from; x < to; x++)
        {
            for (var y = 0; y < surface.Height; y++)
            {
                var pixel = surface.GetPixel(x, y);
                if (pixel.A < 250)
                {
                    continue;
                }

                if (pixel.R < 60)
                {
                    dark++;
                }
                else if (pixel.R > 200)
                {
                    light++;
                }
            }
        }

        return (dark, light);
    }

    [Fact]
    public void MissingWallpaperFallsBackToTheFillColour()
    {
        var monitor = new Rectangle(0, 0, 400, 300);
        var source = new WallpaperSource(null, WallpaperFit.Fill, Color.FromArgb(10, 20, 30), monitor, monitor);

        var (backdrop, analysis) = BandBackdrop.Build(source, new Rectangle(0, 0, 400, 24), blur: false);

        Assert.Null(backdrop);
        Assert.Equal(Color.FromArgb(10, 20, 30).ToArgb(), analysis.Mean.ToArgb());
        Assert.Equal(0d, analysis.Complexity, 6);
    }
}
