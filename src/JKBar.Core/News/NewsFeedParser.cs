// Parses RSS and Atom without retaining article bodies or other feed content.
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace JKBar.Core.News;

public static class NewsFeedParser
{
    public static IReadOnlyList<NewsItem> Parse(string xml, Uri feedUri)
    {
        XDocument document;
        try
        {
            document = Load(xml);
        }
        catch (XmlException)
        {
            var repaired = RepairBareAmpersands(xml);
            if (string.Equals(repaired, xml, StringComparison.Ordinal))
            {
                throw;
            }

            document = Load(repaired);
        }

        var root = document.Root ?? throw new XmlException("The feed has no root element.");

        return root.Name.LocalName switch
        {
            "rss" => ParseRss(root, feedUri),
            "feed" => ParseAtom(root, feedUri),
            _ => throw new XmlException($"Unsupported feed root '{root.Name.LocalName}'.")
        };
    }

    private static XDocument Load(string xml)
    {
        using var text = new StringReader(xml);
        using var reader = XmlReader.Create(text, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });

        return XDocument.Load(reader, LoadOptions.None);
    }

    private static string RepairBareAmpersands(string xml)
    {
        StringBuilder? repaired = null;
        var copiedUntil = 0;

        for (var index = 0; index < xml.Length; index++)
        {
            var terminator = xml.AsSpan(index).StartsWith("<![CDATA[", StringComparison.Ordinal)
                ? "]" + "]>"
                : xml.AsSpan(index).StartsWith("<!--", StringComparison.Ordinal)
                    ? "-->"
                    : xml.AsSpan(index).StartsWith("<?", StringComparison.Ordinal)
                        ? "?>"
                        : null;
            if (terminator is not null)
            {
                var end = xml.IndexOf(terminator, index + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    return xml;
                }

                index = end + terminator.Length - 1;
                continue;
            }

            if (xml[index] != '&' || IsXmlEntityAt(xml, index))
            {
                continue;
            }

            repaired ??= new StringBuilder(xml.Length + 16);
            repaired.Append(xml, copiedUntil, index - copiedUntil);
            repaired.Append("&amp;");
            copiedUntil = index + 1;
        }

        if (repaired is null)
        {
            return xml;
        }

        repaired.Append(xml, copiedUntil, xml.Length - copiedUntil);
        return repaired.ToString();
    }

    private static bool IsXmlEntityAt(string xml, int ampersand)
    {
        var value = xml.AsSpan(ampersand);
        if (value.StartsWith("&amp;", StringComparison.Ordinal)
            || value.StartsWith("&lt;", StringComparison.Ordinal)
            || value.StartsWith("&gt;", StringComparison.Ordinal)
            || value.StartsWith("&quot;", StringComparison.Ordinal)
            || value.StartsWith("&apos;", StringComparison.Ordinal))
        {
            return true;
        }

        if (value.Length < 4 || value[1] != '#')
        {
            return false;
        }

        var index = 2;
        var hexadecimal = index < value.Length && value[index] is 'x' or 'X';
        if (hexadecimal)
        {
            index++;
        }

        var firstDigit = index;
        while (index < value.Length && (hexadecimal
                   ? Uri.IsHexDigit(value[index])
                   : char.IsAsciiDigit(value[index])))
        {
            index++;
        }

        return index > firstDigit && index < value.Length && value[index] == ';';
    }

    private static IReadOnlyList<NewsItem> ParseRss(XElement root, Uri feedUri) =>
        root.Descendants().Where(element => element.Name.LocalName == "item")
            .Select(item => Create(
                ChildValue(item, "title"),
                ChildValue(item, "link"),
                ChildValue(item, "pubDate"),
                feedUri))
            .OfType<NewsItem>()
            .DistinctBy(item => item.Link)
            .ToArray();

    private static IReadOnlyList<NewsItem> ParseAtom(XElement root, Uri feedUri) =>
        root.Elements().Where(element => element.Name.LocalName == "entry")
            .Select(entry => Create(
                ChildValue(entry, "title"),
                AtomLink(entry),
                ChildValue(entry, "updated") ?? ChildValue(entry, "published"),
                feedUri))
            .OfType<NewsItem>()
            .DistinctBy(item => item.Link)
            .ToArray();

    private static NewsItem? Create(string? title, string? link, string? published, Uri feedUri)
    {
        var cleanTitle = CollapseWhitespace(title);
        if (cleanTitle.Length == 0 || !TryCreateHttpUri(feedUri, link, out var uri))
        {
            return null;
        }

        DateTimeOffset? publishedAt = DateTimeOffset.TryParse(
            published,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out var parsed)
                ? parsed
                : null;

        return new NewsItem(cleanTitle, uri, publishedAt);
    }

    private static string? ChildValue(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(element => element.Name.LocalName == localName)?.Value;

    private static string? AtomLink(XElement entry) =>
        entry.Elements()
            .Where(element => element.Name.LocalName == "link")
            .FirstOrDefault(element =>
                element.Attribute("rel") is null
                || string.Equals((string?)element.Attribute("rel"), "alternate", StringComparison.OrdinalIgnoreCase))
            ?.Attribute("href")?.Value;

    private static string CollapseWhitespace(string? value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static bool TryCreateHttpUri(Uri baseUri, string? value, out Uri uri)
    {
        if (Uri.TryCreate(baseUri, value?.Trim(), out var candidate)
            && (candidate.Scheme == Uri.UriSchemeHttp || candidate.Scheme == Uri.UriSchemeHttps))
        {
            uri = candidate;
            return true;
        }

        uri = null!;
        return false;
    }
}