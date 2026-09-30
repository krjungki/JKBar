// Asks each configured endpoint whether it answers, all at once.
using System.Net.Http.Headers;
using JKBar.Core;
using JKBar.Core.Network;

namespace JKBar.App.Network;

internal sealed class ConnectivityProbe : IDisposable
{
    private readonly HttpClient _client = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(5),
        MaxResponseContentBufferSize = 4096
    };

    internal ConnectivityProbe()
    {
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("JKBar", BuildInfo.Version));
        _client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
    }

    internal async Task<ConnectivityReport> CheckAsync(IReadOnlyList<Uri> probes, CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(probes.Select(async probe =>
            new ProbeResult(probe.Host, await ReachableAsync(probe, cancellationToken))));

        return ConnectivityVerdict.From(results);
    }

    public void Dispose() => _client.Dispose();

    private async Task<bool> ReachableAsync(Uri probe, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _client.GetAsync(probe, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return ConnectivityVerdict.IsSuccess(probe, (int)response.StatusCode);
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
