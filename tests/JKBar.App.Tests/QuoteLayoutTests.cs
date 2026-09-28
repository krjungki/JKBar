// Checks that the quote keeps its outline whichever symbol and reading it shows.
using System.Drawing.Imaging;
using JKBar.App.Rendering;
using JKBar.Core.Layout;
using JKBar.Core.Settings;
using JKBar.Core.Stocks;

namespace JKBar.App.Tests;

public sealed class QuoteLayoutTests
{
    private const int NotchLeft = 700;

    [Fact]
    public void QuoteStartsAndEndsInTheSamePlaceForAnySymbol()
    {
        using var blank = Render(null);
        var shortName = InkExtent(Render(new StockQuote("KOSDAQ", "KOSDAQ", "846.58", "0.00", StockDirection.Flat)), blank);
        var longName = InkExtent(Render(new StockQuote("396500", "TIGER 반도체TOP10", "11,500", "0.00", StockDirection.Flat)), blank);
        var bigPrice = InkExtent(Render(new StockQuote("000660", "SK하이닉스", "1,768,000", "0.00", StockDirection.Flat)), blank);

        Assert.Equal(shortName, longName);
        Assert.Equal(shortName, bigPrice);
    }

    private static Bitmap Render(StockQuote? quote)
    {
        var surface = new Bitmap(1400, 40, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(surface);
        BandRenderer.Paint(
            g,
            surface.Size,
            new NotchGeometry.Rect(NotchLeft, 0, 200, 40),
            12,
            BandStyle.Default,
            new BandTypographySettings(),
            image: null,
            imageScalePercent: 100,
            activeApp: null,
            quote: quote,
            news: null,
            items: [],
            runningProcesses: []);
        return surface;
    }

    private static (int Left, int Right) InkExtent(Bitmap surface, Bitmap blank)
    {
        using (surface)
        {
            int left = int.MaxValue, right = int.MinValue;
            for (var x = 0; x < NotchLeft; x++)
            {
                for (var y = 0; y < surface.Height; y++)
                {
                    if (surface.GetPixel(x, y) != blank.GetPixel(x, y))
                    {
                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                    }
                }
            }

            return (left, right);
        }
    }
}
