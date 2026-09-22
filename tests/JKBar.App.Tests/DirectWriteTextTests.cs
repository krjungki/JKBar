// Exercises the CPU DirectWrite shaping and glyph-analysis path without creating a Direct2D or D3D device.
using System.Diagnostics;
using System.Drawing.Imaging;
using JKBar.App.Rendering;
using JKBar.Core.Layout;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;

namespace JKBar.App.Tests;

public sealed class DirectWriteTextTests
{
    [Fact]
    public void MeasuresShapedLatinAndKoreanTextIncludingTrailingSpaces()
    {
        using var font = new Font("Segoe UI", 15.2f, FontStyle.Bold, GraphicsUnit.Pixel);

        var mixed = DirectWriteText.Measure("NEWS 한글 English", font);
        var withoutSpaces = DirectWriteText.Measure("CPU", font);
        var withSpaces = DirectWriteText.Measure("CPU  ", font);

        Assert.True(DirectWriteText.IsAvailable);
        Assert.InRange(mixed, 80, 300);
        Assert.True(withSpaces > withoutSpaces);
    }

    [Fact]
    public void DrawsCentredEllipsizedTextIntoTheRequestedBounds()
    {
        using var surface = new Bitmap(260, 40, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(surface);
        using var font = new Font("Segoe UI", 15.2f, FontStyle.Bold, GraphicsUnit.Pixel);
        graphics.Clear(Color.Transparent);

        DirectWriteText.Draw(
            graphics,
            "NEWS 14:37 한글과 English 제목이 길어서 잘립니다",
            font,
            Color.FromArgb(255, 24, 24, 27),
            new RectangleF(20, 0, 220, 40),
            StringAlignment.Center,
            StringAlignment.Center,
            ellipsis: true);

        var ink = InkBounds(surface);
        Assert.False(ink.IsEmpty);
        Assert.InRange(ink.Left, 20, 80);
        Assert.InRange(ink.Right, 180, 240);
        Assert.InRange(ink.Top, 5, 18);
        Assert.InRange(ink.Bottom, 22, 36);
        Assert.Equal(0, surface.GetPixel(10, 20).A);
        Assert.Equal(0, surface.GetPixel(250, 20).A);
    }

    [Fact]
    public void RepeatedGlyphAnalysisDoesNotLoadAGraphicsStack()
    {
        using var surface = new Bitmap(500, 56, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(surface);
        using var font = new Font("Segoe UI", 21.28f, FontStyle.Bold, GraphicsUnit.Pixel);

        for (var index = 0; index < 50; index++)
        {
            DirectWriteText.Draw(
                graphics,
                $"NEWS 한글 English {index}",
                font,
                Color.White,
                new RectangleF(0, 0, surface.Width, surface.Height),
                StringAlignment.Center,
                StringAlignment.Center,
                ellipsis: true);
        }

        var forbidden = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
            .Select(module => module.ModuleName ?? string.Empty)
            .Where(name =>
                name.Equals("d2d1.dll", StringComparison.OrdinalIgnoreCase)
                || name.Equals("d3d11.dll", StringComparison.OrdinalIgnoreCase)
                || name.Equals("D3D10Warp.dll", StringComparison.OrdinalIgnoreCase)
                || name.Equals("dxgi.dll", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void ProductRenderersUseTheCpuTextPathWithoutAGraphicsStack()
    {
        using var notch = new Bitmap(336, 56, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(notch))
        {
            NotchRenderer.Paint(
                graphics,
                new NotchMetrics(336, 56, 14),
                Color.Black,
                new NotchContent("OneDrive 주의", "동기화 상태를 확인하세요"),
                new BandTypographySettings(),
                14f);
        }

        using var band = new Bitmap(1200, 56, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(band))
        {
            BandRenderer.Paint(
                graphics,
                band.Size,
                new NotchGeometry.Rect(500, 0, 700, 56),
                14,
                BandStyle.Default,
                new BandTypographySettings(),
                image: null,
                imageScalePercent: 100,
                activeApp: "Visual Studio Code",
                quote: null,
                news: null,
                items:
                [
                    new BandItem("CPU", "37%", "100%") { Layout = BandItemLayout.StackedPercent },
                    new BandItem("NET", [new BandValue("1 KB/s", "1023 MB/s"), new BandValue("2 KB/s", "1023 MB/s")])
                    {
                        Layout = BandItemLayout.RateRows,
                        Kind = BandItemKind.Network
                    },
                    new BandItem("Tue 22", "14:37", "00:00") { Kind = BandItemKind.Clock }
                ],
                runningProcesses: []);
        }

        Assert.InRange(CountDistinct(notch), 20, 5_000);
        Assert.InRange(CountDistinct(band), 20, 20_000);
        Assert.False(InkBounds(notch).IsEmpty);
        Assert.Empty(ForbiddenGraphicsModules());
    }

    [Theory]
    [InlineData(21)]
    [InlineData(26)]
    [InlineData(37)]
    public void StatusBadgesRenderThreeDistinctMarksAtSupportedBandSizes(int iconDiameter)
    {
        var hashes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var badge in new[] { BandItemBadge.Good, BandItemBadge.Synchronizing, BandItemBadge.Attention })
        {
            using var bitmap = new Bitmap(56, 56, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            BandRenderer.DrawStatusBadge(
                graphics,
                new RectangleF(8, 8, iconDiameter, iconDiameter),
                iconDiameter,
                badge);

            Assert.False(InkBounds(bitmap).IsEmpty);
            hashes.Add(PixelHash(bitmap));
        }

        Assert.Equal(3, hashes.Count);
    }

    [Fact]
    public void SynchronizingBadgeUsesYellowAndADarkTwoArrowMark()
    {
        using var bitmap = new Bitmap(56, 56, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        BandRenderer.DrawStatusBadge(graphics, new RectangleF(8, 8, 37, 37), 37, BandItemBadge.Synchronizing);

        var colours = Enumerable.Range(0, bitmap.Height)
            .SelectMany(y => Enumerable.Range(0, bitmap.Width).Select(x => bitmap.GetPixel(x, y)))
            .Where(colour => colour.A > 0)
            .ToArray();
        Assert.Contains(colours, colour => colour.R == 255 && colour.G == 185 && colour.B == 0);
        Assert.Contains(colours, colour => colour.R == 36 && colour.G == 36 && colour.B == 40);
    }

    private static Rectangle InkBounds(Bitmap bitmap)
    {
        var left = bitmap.Width;
        var top = bitmap.Height;
        var right = -1;
        var bottom = -1;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A == 0)
                {
                    continue;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        return right < left ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    private static int CountDistinct(Bitmap bitmap)
    {
        var colours = new HashSet<int>();
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                colours.Add(bitmap.GetPixel(x, y).ToArgb());
            }
        }

        return colours.Count;
    }

    private static string PixelHash(Bitmap bitmap)
    {
        var bytes = new byte[bitmap.Width * bitmap.Height * sizeof(int)];
        var offset = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                BitConverter.TryWriteBytes(bytes.AsSpan(offset, sizeof(int)), bitmap.GetPixel(x, y).ToArgb());
                offset += sizeof(int);
            }
        }

        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }

    private static string[] ForbiddenGraphicsModules() => Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
        .Select(module => module.ModuleName ?? string.Empty)
        .Where(name =>
            name.Equals("d2d1.dll", StringComparison.OrdinalIgnoreCase)
            || name.Equals("d3d11.dll", StringComparison.OrdinalIgnoreCase)
            || name.Equals("D3D10Warp.dll", StringComparison.OrdinalIgnoreCase)
            || name.Equals("dxgi.dll", StringComparison.OrdinalIgnoreCase))
        .ToArray();
}