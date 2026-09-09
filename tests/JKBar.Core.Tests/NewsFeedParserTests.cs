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

    [Fact]
    public void RepairsABareAmpersandInAnAttributeLikeTheYnaFeed()
    {
        const string xml = """
            <rss version="2.0" xmlns:media="http://search.yahoo.com/mrss/">
              <channel>
                <item>
                  <title>Valid headline</title>
                  <link>https://example.com/news/3</link>
                  <media:content url="https://video.example/embed/watch?v=abc&amp;safe=1&feature=feed" />
                </item>
              </channel>
            </rss>
            """;

        var item = Assert.Single(NewsFeedParser.Parse(xml, FeedUri));

        Assert.Equal("Valid headline", item.Title);
    }

    [Fact]
    public void RepairsBareAmpersandsInTextWithoutDoubleEscapingEntitiesOrCdata()
    {
        const string xml = """
            <rss version="2.0">
              <channel>
                <item><title>A &amp; B & C</title><link>https://example.com/news/4</link></item>
                <item><title><![CDATA[D & E]]></title><link>https://example.com/news/5</link></item>
                <item><title>F &#38; G &#x26; H</title><link>https://example.com/news/6</link></item>
              </channel>
            </rss>
            """;

        var items = NewsFeedParser.Parse(xml, FeedUri);

        Assert.Equal(["A & B & C", "D & E", "F & G & H"], items.Select(item => item.Title));
    }

    [Fact]
    public void StillRejectsMalformedElementStructure()
    {
        const string xml = "<rss><channel><item><title>Broken & title</item></channel></rss>";

        Assert.Throws<XmlException>(() => NewsFeedParser.Parse(xml, FeedUri));
    }

      [Fact]
      public void LeavesCommentsAndProcessingInstructionsUntouchedWhileRepairingContent()
      {
        const string xml = """
          <?feed-note source="A&B"?>
          <rss version="2.0">
            <!-- A&B is intentionally literal comment text. -->
            <channel><item><title>A & B</title><link>https://example.com/news/7</link></item></channel>
          </rss>
          """;

        var item = Assert.Single(NewsFeedParser.Parse(xml, FeedUri));

        Assert.Equal("A & B", item.Title);
      }

      [Fact]
      public void DoesNotPartiallyRepairAnUnclosedCdataSection()
      {
        const string xml = "<rss><channel>A & B<![CDATA[unfinished</channel></rss>";

        Assert.Throws<XmlException>(() => NewsFeedParser.Parse(xml, FeedUri));
      }

      [Fact]
      public void LeavesInvalidNumericEntitiesForTheXmlReaderToReject()
      {
        const string xml = "<rss><channel><item><title>&#xFFFFFFFF;</title></item></channel></rss>";

        Assert.Throws<XmlException>(() => NewsFeedParser.Parse(xml, FeedUri));
      }
}