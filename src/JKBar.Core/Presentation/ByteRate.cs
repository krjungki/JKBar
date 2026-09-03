// Formats throughput for a band that is read at a glance, not studied.
using System.Globalization;

namespace JKBar.Core.Presentation;

public static class ByteRate
{
    private const double Kilobyte = 1024d;
    private const double Megabyte = Kilobyte * 1024d;
    private const double Gigabyte = Megabyte * 1024d;

    /// <summary>
    /// Whole units below a megabyte. A readout jittering between 1013 and 1021 B/s carries no information the
    /// eye can use, and a changing string forces the whole band to repaint.
    /// </summary>
    public static string PerSecond(double bytesPerSecond)
    {
        var value = double.IsFinite(bytesPerSecond) ? Math.Max(0, bytesPerSecond) : 0;

        return value switch
        {
            < Kilobyte => Format(value, 0, "B/s"),
            < Megabyte => Format(value / Kilobyte, 0, "KB/s"),
            < Gigabyte => Format(value / Megabyte, 1, "MB/s"),
            _ => Format(value / Gigabyte, 1, "GB/s")
        };
    }

    public static string Percent(double percent)
    {
        var value = double.IsFinite(percent) ? Math.Clamp(percent, 0, 100) : 0;

        return value.ToString("0", CultureInfo.InvariantCulture) + "%";
    }

    private static string Format(double value, int decimals, string unit) =>
        value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) + " " + unit;
}
