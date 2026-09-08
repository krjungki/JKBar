// Formats the optional resting-notch date and time without reading the system clock directly.
using System.Globalization;
using JKBar.Core.Settings;

namespace JKBar.Core.Presentation;

public static class IdleNotchSource
{
    public static string Text(DateTimeOffset now, CultureInfo culture) =>
        $"{ClockSource.Date(now)}  {now.ToString("HH:mm", culture)}";

    public static string? Content(
        IdleNotchContent idleContent,
        bool hasActiveAlert,
        DateTimeOffset now,
        CultureInfo culture) => idleContent == IdleNotchContent.DateTime && !hasActiveAlert
            ? Text(now, culture)
            : null;

    public static string Signature(DateTimeOffset now) =>
        now.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
}