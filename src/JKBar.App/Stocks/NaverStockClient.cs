// Reads Naver's finance endpoints. These serve the site rather than a documented API, so every failure is
// swallowed into an empty result and the band simply shows nothing.
using System.Net;
using System.Net.Http.Headers;
using JKBar.Core;
using JKBar.Core.Settings;
using JKBar.Core.Stocks;

namespace JKBar.App.Stocks;

internal sealed class NaverStockClient : IDisposable
{
    private const long MaximumBytes = 512 * 1024;
    private const string SearchAddress = "https://ac.stock.naver.com/ac?target=stock%2Cindex&q=";
    private const string QuoteAddress = "https://polling.finance.naver.com/api/realtime/domestic/";

    private readonly HttpClient _client = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
        MaxResponseContentBufferSize = MaximumBytes
    };

    internal NaverStockClient()
    {
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("JKBar", BuildInfo.Version));
        _client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    /// <returns>Null when Naver could not be reached, which is not the same as a name it does not know.</returns>
    internal async Task<IReadOnlyList<StockMatch>?> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            return [];
        }

        var json = await ReadAsync(SearchAddress + WebUtility.UrlEncode(trimmed), cancellationToken);

        return json is null ? null : NaverStockParser.Matches(json);
    }

    /// <summary>
    /// Indexes and shares are quoted through different paths, so at most two requests are made no matter how
    /// many symbols are registered.
    /// </summary>
    internal async Task<IReadOnlyList<StockQuote>> QuotesAsync(
        IReadOnlyList<WatchedStock> watched,
        CancellationToken cancellationToken)
    {
        if (watched.Count == 0)
        {
            return [];
        }

        var names = watched
            .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);
        var quotes = new List<StockQuote>();

        foreach (var group in watched.GroupBy(item => item.IsIndex))
        {
            var path = group.Key ? "index/" : "stock/";
            var codes = string.Join(',', group.Select(item => WebUtility.UrlEncode(item.Code)));
            if (await ReadAsync(QuoteAddress + path + codes, cancellationToken) is { } json)
            {
                quotes.AddRange(NaverStockParser.Quotes(json, names));
            }
        }

        // Registration order is what the user arranged, and the reply does not have to preserve it.
        return watched
            .Select(item => quotes.FirstOrDefault(
                quote => string.Equals(quote.Code, item.Code, StringComparison.OrdinalIgnoreCase)))
            .OfType<StockQuote>()
            .ToArray();
    }

    private async Task<string?> ReadAsync(string address, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _client.GetAsync(
                address,
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return null;
        }
    }

    public void Dispose() => _client.Dispose();
}
