// Formats throughput for a band that is read at a glance, not studied.
using System.Globalization;

namespace JKBar.Core.Presentation;

public static class ByteRate
{
    private const double Step = 1024d;
    private const double WidestNumber = 9999d;

    private static readonly string[] Units = ["B/s", "KB/s", "MB/s", "GB/s", "TB/s"];

    /// <summary>
    /// Widest string <see cref="PerSecond"/> can return: four digits, and M is the broadest of the unit letters.
    /// </summary>
    public const string WidestRate = "1023 MB/s";

    public const string WidestPercent = "100%";

    /// <summary>
    /// A decimal only while it still says something: 1.4 MB/s is worth telling from 1.0, 431 MB/s is not.
    /// Kept inside <see cref="WidestRate"/> so layout can reserve a fixed box and a reading stops shifting
    /// its neighbours.
    /// </summary>
    public static string PerSecond(double bytesPerSecond)
    {
        var value = double.IsFinite(bytesPerSecond) ? Math.Max(0, bytesPerSecond) : 0;
        var unit = 0;

        while (value >= Step && unit < Units.Length - 1)
        {
            value /= Step;
            unit++;
        }

        // Past this the figure is not a reading of anything, but the box still has to hold whatever is printed.
        value = Math.Min(value, WidestNumber);

        var digits = unit > 0 && value < 10 ? "0.0" : "0";

        return value.ToString(digits, CultureInfo.InvariantCulture) + " " + Units[unit];
    }

    public static string Percent(double percent)
    {
        var value = double.IsFinite(percent) ? Math.Clamp(percent, 0, 100) : 0;

        return value.ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}
