// Checks that the news and stock icons draw inside the area they were given, at the sizes the band uses.
using JKBar.App.Rendering;

namespace JKBar.App.Tests;

public sealed class BandIconsTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(16)]
    [InlineData(28)]
    public void NewsIconDrawsInsideItsArea(int size) => AssertDrawsInside(size, BandIcons.DrawNews);

    [Theory]
    [InlineData(10)]
    [InlineData(16)]
    [InlineData(28)]
    public void StockIconDrawsInsideItsArea(int size) => AssertDrawsInside(size, BandIcons.DrawStock);

    private static void AssertDrawsInside(int size, Action<Graphics, RectangleF> draw)
    {
        const int margin = 6;
        using var bitmap = new Bitmap(size + (margin * 2), size + (margin * 2));
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            draw(g, new RectangleF(margin, margin, size, size));
        }

        var inked = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A == 0)
                {
                    continue;
                }

                // Half a stroke may spill past the edge; anything further means the icon was mis-scaled.
                Assert.InRange(x, margin - 2, margin + size + 1);
                Assert.InRange(y, margin - 2, margin + size + 1);
                inked++;
            }
        }

        Assert.True(inked > size * size / 6, $"Only {inked} pixels were drawn for a {size}px icon.");
    }
}
