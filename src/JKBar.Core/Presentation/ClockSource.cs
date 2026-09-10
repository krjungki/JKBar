// The clock item. Time is passed in rather than read here so the formatting can be tested without waiting for a tick.
using System.Globalization;

namespace JKBar.Core.Presentation;

public static class ClockSource
{
    public static BandItem Item(DateTimeOffset now, CultureInfo culture) =>
        new(
            Date(now),
            [new BandValue(now.ToString("HH:mm", culture), "00:00")],
            LabelTemplate: WidestDate(),
            Kind: BandItemKind.Clock);

    /// <summary>Weekday and day of month only; the month is left out to keep the band short.</summary>
    public static string Date(DateTimeOffset now) =>
        $"{EnglishDay(now.DayOfWeek)} {now.Day.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// What the item would read. The band repaints a full-width surface, so it is worth knowing that nothing
    /// visible changed between one second and the next.
    /// </summary>
    public static string Signature(DateTimeOffset now) =>
        now.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);

    /// <summary>
    /// The date is the only label here that changes, and it sits at the right-hand end where a width change would
    /// push every other reading sideways. Built from the longest weekday name rather than today's.
    /// </summary>
    private static string WidestDate() => "WWW 30";

    private static string EnglishDay(DayOfWeek day) =>
        CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedDayNames[(int)day];
}
