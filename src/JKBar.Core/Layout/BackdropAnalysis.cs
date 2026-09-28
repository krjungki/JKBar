// Measures a strip of wallpaper and filters it into a frosted backdrop. Pixels are 32bpp BGRA rows without padding.
using System.Drawing;

namespace JKBar.Core.Layout;

/// <param name="Mean">Average sRGB colour, which is what compositing a tint over the strip mixes with.</param>
/// <param name="DarkLuminance">Relative luminance of the darkest tenth of the strip.</param>
/// <param name="LightLuminance">Relative luminance of the brightest tenth of the strip.</param>
/// <param name="Complexity">0 for a flat colour, towards 1 for busy detail that fights with text.</param>
public readonly record struct BackdropAnalysis(Color Mean, double DarkLuminance, double LightLuminance, double Complexity)
{
    // Tuned on quarter-resolution strips: a smooth gradient lands near 0.1, a detailed photo past 0.6.
    private const double GradientWeight = 6;
    private const double SpreadWeight = 1.5;

    public static BackdropAnalysis Analyze(ReadOnlySpan<byte> bgra, int width, int height)
    {
        var count = width * height;
        if (count <= 0 || bgra.Length < count * 4)
        {
            throw new ArgumentException("pixel buffer does not match its size", nameof(bgra));
        }

        long red = 0, green = 0, blue = 0;
        var luminance = new double[count];
        var luma = new double[count];
        for (var index = 0; index < count; index++)
        {
            var b = bgra[index * 4];
            var g = bgra[index * 4 + 1];
            var r = bgra[index * 4 + 2];
            red += r;
            green += g;
            blue += b;
            luminance[index] = 0.2126 * ColourContrast.Linear(r) + 0.7152 * ColourContrast.Linear(g)
                + 0.0722 * ColourContrast.Linear(b);
            // Detail is judged on the encoded values, which track perceived lightness far better than linear light.
            luma[index] = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255d;
        }

        var mean = Color.FromArgb(
            (int)Math.Round(red / (double)count),
            (int)Math.Round(green / (double)count),
            (int)Math.Round(blue / (double)count));

        var average = luma.Average();
        var spread = Math.Sqrt(luma.Sum(value => (value - average) * (value - average)) / count);

        double gradient = 0;
        var steps = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var here = luma[y * width + x];
                if (x + 1 < width)
                {
                    gradient += Math.Abs(luma[y * width + x + 1] - here);
                    steps++;
                }

                if (y + 1 < height)
                {
                    gradient += Math.Abs(luma[(y + 1) * width + x] - here);
                    steps++;
                }
            }
        }

        gradient = steps == 0 ? 0 : gradient / steps;
        Array.Sort(luminance);

        return new BackdropAnalysis(
            mean,
            luminance[(int)((count - 1) * 0.1)],
            luminance[(int)((count - 1) * 0.9)],
            Math.Clamp(GradientWeight * gradient + SpreadWeight * spread, 0d, 1d));
    }

    /// <summary>Three box passes each way approximate a Gaussian. Edges repeat their last pixel; alpha is untouched.</summary>
    public static void Blur(Span<byte> bgra, int width, int height, int radius)
    {
        if (radius <= 0 || width <= 0 || height <= 0)
        {
            return;
        }

        var scratch = new byte[bgra.Length];
        for (var pass = 0; pass < 3; pass++)
        {
            BoxPass(bgra, scratch, width, height, radius, horizontal: true);
            BoxPass(scratch, bgra, width, height, radius, horizontal: false);
        }
    }

    /// <summary>Pushes each pixel away from its own grey, the way a vibrant material lifts the colour it shows through.</summary>
    public static void Saturate(Span<byte> bgra, double amount)
    {
        for (var index = 0; index + 3 < bgra.Length; index += 4)
        {
            var b = bgra[index];
            var g = bgra[index + 1];
            var r = bgra[index + 2];
            var grey = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            bgra[index] = Clamp(grey + (b - grey) * amount);
            bgra[index + 1] = Clamp(grey + (g - grey) * amount);
            bgra[index + 2] = Clamp(grey + (r - grey) * amount);
        }
    }

    private static void BoxPass(ReadOnlySpan<byte> source, Span<byte> target, int width, int height, int radius, bool horizontal)
    {
        var lines = horizontal ? height : width;
        var length = horizontal ? width : height;
        var window = radius * 2 + 1;

        for (var line = 0; line < lines; line++)
        {
            for (var channel = 0; channel < 3; channel++)
            {
                var sum = 0;
                for (var offset = -radius; offset <= radius; offset++)
                {
                    sum += source[Offset(line, Math.Clamp(offset, 0, length - 1), width, horizontal) + channel];
                }

                for (var position = 0; position < length; position++)
                {
                    var at = Offset(line, position, width, horizontal) + channel;
                    target[at] = (byte)((sum + window / 2) / window);

                    var leaving = Math.Clamp(position - radius, 0, length - 1);
                    var entering = Math.Clamp(position + radius + 1, 0, length - 1);
                    sum += source[Offset(line, entering, width, horizontal) + channel]
                        - source[Offset(line, leaving, width, horizontal) + channel];
                }
            }

            for (var position = 0; position < length; position++)
            {
                var at = Offset(line, position, width, horizontal) + 3;
                target[at] = source[at];
            }
        }
    }

    private static int Offset(int line, int position, int width, bool horizontal) =>
        horizontal ? (line * width + position) * 4 : (position * width + line) * 4;

    private static byte Clamp(double value) => (byte)Math.Clamp((int)Math.Round(value), 0, 255);
}
