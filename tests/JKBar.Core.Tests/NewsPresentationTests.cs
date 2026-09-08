// Regression checks for feed timestamps shown in the band.
using System.Globalization;
using JKBar.Core.News;

namespace JKBar.Core.Tests;

public class NewsPresentationTests
{
    private static readonly Uri Link = new("https://example.test/article");

    [Fact]
    public void ShowsPostingTimeBeforeTheHeadline()
    {
        var item = new NewsItem("새 소식", Link, new DateTimeOffset(2026, 9, 7, 5, 35, 0, TimeSpan.Zero));
        var korea = TimeZoneInfo.CreateCustomTimeZone("Test/Korea", TimeSpan.FromHours(9), "Korea", "Korea");

        Assert.Equal("NEWS 14:35 [새 소식]", NewsPresentation.Headline(item, korea, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void KeepsTheOldFormatWhenTheFeedHasNoPostingTime()
    {
        var item = new NewsItem("시각 없는 소식", Link, null);

        Assert.Equal("NEWS [시각 없는 소식]", NewsPresentation.Headline(item, TimeZoneInfo.Utc, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void UsesTwentyFourHourTimeAcrossDateBoundaries()
    {
        var item = new NewsItem("자정 뉴스", Link, new DateTimeOffset(2026, 9, 7, 23, 5, 0, TimeSpan.Zero));
        var korea = TimeZoneInfo.CreateCustomTimeZone("Test/KoreaMidnight", TimeSpan.FromHours(9), "Korea", "Korea");

        Assert.Equal("NEWS 08:05 [자정 뉴스]", NewsPresentation.Headline(item, korea, CultureInfo.InvariantCulture));
    }
}