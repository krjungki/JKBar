// Downloads bounded RSS or Atom documents and hands parsing to the dependency-free core parser.
using System.Net.Http.Headers;
using JKBar.Core;
using JKBar.Core.News;

namespace JKBar.App;

internal sealed class NewsFeedClient : IDisposable
{
    private const long MaximumFeedBytes = 2 * 1024 * 1024;
    private readonly HttpClient _client = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
        MaxResponseContentBufferSize = MaximumFeedBytes
    };

    internal NewsFeedClient()
    {
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("JKBar", BuildInfo.Version));
        _client.DefaultRequestHeaders.Accept.ParseAdd("application/rss+xml, application/atom+xml, application/xml, text/xml");
    }

    internal async Task<IReadOnlyList<NewsItem>> FetchAsync(Uri feedUri, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(
            feedUri,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        return NewsFeedParser.Parse(xml, feedUri);
    }

    public void Dispose() => _client.Dispose();
}