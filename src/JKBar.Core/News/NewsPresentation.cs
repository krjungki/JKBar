// Formats the compact feed text shown in the band. The band draws its own news icon in front of it.
using System.Globalization;

namespace JKBar.Core.News;

public static class NewsPresentation
{
    public static string Headline(NewsItem item, TimeZoneInfo localZone, CultureInfo culture) =>
        Time(item, localZone, culture) is { } time ? $"{time} {Title(item)}" : Title(item);

    /// <summary>The local publication time, or null when the feed gave none.</summary>
    public static string? Time(NewsItem item, TimeZoneInfo localZone, CultureInfo culture) =>
        item.PublishedAt is { } published
            ? TimeZoneInfo.ConvertTime(published, localZone).ToString("HH:mm", culture)
            : null;

    public static string Title(NewsItem item) => $"[{item.Title}]";
}