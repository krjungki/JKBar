// The clock item. Time is passed in rather than read here so the formatting can be tested without waiting for a tick.
using System.Globalization;

namespace JKBar.Core.Presentation;

public static class ClockSource
{
    public static BandItem Item(DateTimeOffset now, CultureInfo culture) =>
        new(now.ToString("ddd d MMM", culture), now.ToString("HH:mm", culture));

    /// <summary>
    /// What the item would read. The band repaints a full-width surface, so it is worth knowing that nothing
    /// visible changed between one second and the next.
    /// </summary>
    public static string Signature(DateTimeOffset now) =>
        now.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
}
