// The clock item. Time is passed in rather than read here so the formatting can be tested without waiting for a tick.
using System.Globalization;

namespace JKBar.Core.Presentation;

public static class ClockSource
{
    private const string DateFormat = "ddd d MMM";

    public static BandItem Item(DateTimeOffset now, CultureInfo culture) =>
        new(
            now.ToString(DateFormat, culture),
            [new BandValue(now.ToString("HH:mm", culture), "00:00")],
            LabelTemplate: WidestDate(culture));

    /// <summary>
    /// What the item would read. The band repaints a full-width surface, so it is worth knowing that nothing
    /// visible changed between one second and the next.
    /// </summary>
    public static string Signature(DateTimeOffset now) =>
        now.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);

    /// <summary>
    /// The date is the only label here that changes, and it sits at the right-hand end where a width change would
    /// push every other reading sideways. Built from the culture's longest names rather than today's.
    /// </summary>
    private static string WidestDate(CultureInfo culture)
    {
        var format = culture.DateTimeFormat;
        var day = Longest(format.AbbreviatedDayNames);
        var month = Longest(format.AbbreviatedMonthNames);

        return $"{day} 30 {month}";
    }

    private static string Longest(IEnumerable<string> names) =>
        names.Where(name => name.Length > 0).OrderByDescending(name => name.Length).FirstOrDefault() ?? string.Empty;
}
