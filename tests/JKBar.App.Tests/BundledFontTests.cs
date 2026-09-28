// Checks that the bundled Pretendard faces load from memory and render through DirectWrite without installation.
using System.Diagnostics;
using System.Drawing.Imaging;
using JKBar.App.Rendering;
using JKBar.Core.Settings;

namespace JKBar.App.Tests;

public sealed class BundledFontTests
{
    [Theory]
    [InlineData("Pretendard", 400)]
    [InlineData("Pretendard Light", 300)]
    [InlineData("pretendard medium", 500)]
    [InlineData("Pretendard SemiBold", 600)]
    [InlineData("Pretendard Bold", 700)]
    public void BundledNamesMapToTheirWeight(string family, int weight)
    {
        Assert.Equal(weight, DirectWriteEngine.BundledWeight(family));
    }

    [Theory]
    [InlineData("Segoe UI")]
    [InlineData("Pretendard Black")]
    [InlineData("Pretendard JP")]
    public void OtherNamesAreLeftToTheSystem(string family)
    {
        Assert.Null(DirectWriteEngine.BundledWeight(family));
    }

    [Fact]
    public void DefaultFontIsTheBundledSemiBold()
    {
        Assert.Equal(600, DirectWriteEngine.BundledWeight(BandTypographySettings.DefaultFontFamily));
    }

    [Fact]
    public void BundledFacesLoadAndDrawHeavierAsTheWeightRises()
    {
        Assert.True(DirectWriteText.HasBundledFont);

        using var regular = new Font("Pretendard Regular", 20f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var semiBold = new Font("Pretendard SemiBold", 20f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var system = new Font("Segoe UI", 20f, FontStyle.Regular, GraphicsUnit.Pixel);
        const string sample = "JKBar 한글 21:39 Mon 28";

        var regularInk = Ink(sample, regular);
        var semiBoldInk = Ink(sample, semiBold);

        Assert.True(semiBoldInk > regularInk * 1.05, $"regular {regularInk}, semibold {semiBoldInk}");
        Assert.NotEqual(DirectWriteText.Measure(sample, system), DirectWriteText.Measure(sample, regular));

        var forbidden = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
            .Select(module => module.ModuleName ?? string.Empty)
            .Where(name => name.Equals("d2d1.dll", StringComparison.OrdinalIgnoreCase)
                || name.Equals("d3d11.dll", StringComparison.OrdinalIgnoreCase)
                || name.Equals("D3D10Warp.dll", StringComparison.OrdinalIgnoreCase)
                || name.Equals("dxgi.dll", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(forbidden);
    }

    /// <summary>Total alpha laid down, which grows with stroke weight for the same text and size.</summary>
    private static long Ink(string text, Font font)
    {
        using var surface = new Bitmap(420, 40, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(surface))
        {
            g.Clear(Color.Transparent);
            DirectWriteText.Draw(g, text, font, Color.Black, new RectangleF(0, 0, 420, 40),
                StringAlignment.Near, StringAlignment.Center, ellipsis: false);
        }

        long total = 0;
        for (var x = 0; x < surface.Width; x++)
        {
            for (var y = 0; y < surface.Height; y++)
            {
                total += surface.GetPixel(x, y).A;
            }
        }

        return total;
    }
}
