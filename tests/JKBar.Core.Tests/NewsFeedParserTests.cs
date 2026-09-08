// Verifies the supported RSS and Atom shapes without making live network requests.
using System.Xml;
using JKBar.Core.News;

namespace JKBar.Core.Tests;

public class NewsFeedParserTests
{
    private static readonly Uri FeedUri = new("https://example.com/feed.xml");

    [Fact]
    public void ParsesRssItemsAndCollapsesTitleWhitespace()
    {
        const string xml = """
            <rss version="2.0">
              <channel>
                <item>
                  <title><![CDATA[ First   headline ]]></title>
                  <link>https://example.com/news/1</link>
                  <pubDate>Mon, 7 Sep 2026 10:03:02 +0900</pubDate>
                </item>
              </channel>
            </rss>
            """;

        var item = Assert.Single(NewsFeedParser.Parse(xml, FeedUri));

        Assert.Equal("First headline", item.Title);
        Assert.Equal("https://example.com/news/1", item.Link.AbsoluteUri);
        Assert.NotNull(item.PublishedAt);
    }

    [Fact]
    public void ParsesNamespacedAtomAndRelativeLinks()
    {
        const string xml = """
            <feed xmlns="http://www.w3.org/2005/Atom">
              <entry>
                <title>Atom headline</title>
                <link rel="alternate" href="/news/2" />
                <updated>2026-09-07T01:03:02Z</updated>
              </entry>
            </feed>
            """;

        var item = Assert.Single(NewsFeedParser.Parse(xml, FeedUri));

        Assert.Equal("https://example.com/news/2", item.Link.AbsoluteUri);
    }

    [Fact]
    public void SkipsEntriesWithoutAnHttpLink()
    {
        const string xml = """
            <rss version="2.0"><channel><item><title>Unsafe</title><link>file:///c:/secret.txt</link></item></channel></rss>
            """;

        Assert.Empty(NewsFeedParser.Parse(xml, FeedUri));
    }

    [Fact]
    public void RejectsDocumentTypeDeclarations()
    {
        const string xml = "<!DOCTYPE rss [<!ENTITY data SYSTEM 'file:///c:/secret.txt'>]><rss version='2.0' />";

        Assert.Throws<XmlException>(() => NewsFeedParser.Parse(xml, FeedUri));
    }
}