// Asks the two endpoints Chrome and Windows already use for their own connectivity checks.
using System.Net;
using System.Net.Http.Headers;
using JKBar.Core;
using JKBar.Core.Network;

namespace JKBar.App.Network;

internal sealed class ConnectivityProbe : IDisposable
{
    private static readonly Uri Google = new("https://www.google.com/generate_204");
    private static readonly Uri Microsoft = new("http://www.msftconnecttest.com/connecttest.txt");

    private readonly HttpClient _client = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
        MaxResponseContentBufferSize = 4096
    };

    internal ConnectivityProbe()
    {
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("JKBar", BuildInfo.Version));
        _client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
    }

    internal async Task<ConnectivityState> CheckAsync(CancellationToken cancellationToken)
    {
        var google = ReachableAsync(Google, HttpStatusCode.NoContent, cancellationToken);
        var microsoft = ReachableAsync(Microsoft, HttpStatusCode.OK, cancellationToken);
        var results = await Task.WhenAll(google, microsoft);

        return ConnectivityVerdict.From(results[0], results[1]);
    }

    public void Dispose() => _client.Dispose();

    private async Task<bool> ReachableAsync(Uri probe, HttpStatusCode expected, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _client.GetAsync(probe, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return response.StatusCode == expected;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }
}
