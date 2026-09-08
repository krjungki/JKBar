// Formats the compact feed text shown in the band.
using System.Globalization;

namespace JKBar.Core.News;

public static class NewsPresentation
{
    public static string Headline(NewsItem item, TimeZoneInfo localZone, CultureInfo culture)
    {
        if (item.PublishedAt is not { } published)
        {
            return $"NEWS [{item.Title}]";
        }

        var local = TimeZoneInfo.ConvertTime(published, localZone);
        return $"NEWS {local.ToString("HH:mm", culture)} [{item.Title}]";
    }
}