// Renders the right-hand readouts with the new spacing and clock options, checking where the ink lands.
using System.Drawing.Imaging;
using JKBar.App.Rendering;
using JKBar.Core.Layout;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;

namespace JKBar.App.Tests;

public sealed class BandLayoutOptionsTests
{
    [Theory]
    [InlineData(BandItemKind.OneDrive, BandItemKind.Network, 250)]
    [InlineData(BandItemKind.Cpu, BandItemKind.GlobalSecureAccess, 250)]
    [InlineData(BandItemKind.Cpu, BandItemKind.Memory, 250)]
    [InlineData(BandItemKind.Disk, BandItemKind.Network, 250)]
    [InlineData(BandItemKind.OneDrive, BandItemKind.Syncthing, 250)]
    [InlineData(BandItemKind.Clock, BandItemKind.Syncthing, 100)]
    [InlineData(BandItemKind.Cpu, BandItemKind.Custom, 100)]
    public void OneSpacingCoversReadoutsIconsAndTheSeamBetween(BandItemKind right, BandItemKind left, int expected)
    {
        Assert.Equal(expected, BandRenderer.GroupSpacing(right, left, new BandSpacing(250)));
    }

    [Fact]
    public void EveryGapInTheRowFollowsTheSameSpacing()
    {
        BandItem[] metrics =
        [
            new("CPU", "37%", "100%") { Kind = BandItemKind.Cpu },
            new("RAM", "63%", "100%") { Kind = BandItemKind.Memory }
        ];

        var tight = InkLeft(Render(metrics, new BandSpacing(0)));
        var standard = InkLeft(Render(metrics, BandSpacing.Standard));
        var loose = InkLeft(Render(metrics, new BandSpacing(500)));

        Assert.True(tight > standard && standard > loose, $"tight {tight}, standard {standard}, loose {loose}");
    }

    [Fact]
    public void WiderSeamPushesTheReadoutsFurtherLeft()
    {
        BandItem[] items =
        [
            new("CPU", "37%", "100%") { Kind = BandItemKind.Cpu },
            new("O", []) { Kind = BandItemKind.OneDrive, Layout = BandItemLayout.StatusIcon }
        ];

        var tight = InkLeft(Render(items, new BandSpacing(0)));
        var standard = InkLeft(Render(items, BandSpacing.Standard));
        var loose = InkLeft(Render(items, new BandSpacing(500)));

        Assert.True(tight > standard && standard > loose, $"tight {tight}, standard {standard}, loose {loose}");
    }

    [Fact]
    public void TwoLineClockDrawsTwoSeparateRows()
    {
        var clock = Assert.Single(ClockSource.Items(
            new DateTimeOffset(2026, 9, 28, 21, 39, 0, TimeSpan.Zero),
            System.Globalization.CultureInfo.InvariantCulture,
            new ClockSettings { TwoLines = true }));

        using var surface = Render([clock], BandSpacing.Standard, height: 56);
        var rows = Enumerable.Range(0, surface.Height)
            .Select(y => Enumerable.Range(600, 200).Any(x => surface.GetPixel(x, y).A > 250 && surface.GetPixel(x, y).R < 80))
            .ToArray();

        // Ink, then a gap of at least one clear row, then ink again.
        var first = Array.IndexOf(rows, true);
        var gap = Array.IndexOf(rows, false, first);
        var second = Array.IndexOf(rows, true, gap);
        Assert.True(first >= 0 && gap > first && second > gap, $"rows {string.Join("", rows.Select(r => r ? '#' : '.'))}");
    }

    private static Bitmap Render(IReadOnlyList<BandItem> items, BandSpacing spacing, int height = 40)
    {
        var surface = new Bitmap(800, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(surface);
        BandRenderer.Paint(
            g,
            surface.Size,
            new NotchGeometry.Rect(0, 0, 100, height),
            12,
            BandStyle.Default,
            new BandTypographySettings(),
            image: null,
            imageScalePercent: 100,
            activeApp: null,
            quote: null,
            news: null,
            items: items,
            runningProcesses: [],
            spacing: spacing);
        return surface;
    }

    private static int InkLeft(Bitmap surface)
    {
        using (surface)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                for (var y = 0; y < surface.Height; y++)
                {
                    var pixel = surface.GetPixel(x, y);
                    if (x >= 100 && pixel.A > 250 && pixel.R < 80)
                    {
                        return x;
                    }
                }
            }

            return -1;
        }
    }
}
