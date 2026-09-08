// Ported from JKMon (packages/JKMon/src/JKMon.Core/Sync). Keep behaviour changes in sync with the original.
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKBar.Core.Sync;

/// <summary>
/// Polls the local Syncthing REST API. Uses the aggregate completion endpoint because the documentation marks
/// per-folder status as an expensive call that should be used sparingly.
/// </summary>
public sealed class SyncthingSyncProvider : ISyncProvider, IDisposable
{
    private readonly HttpClient _http;
    private readonly Func<SyncthingEndpoint?> _endpointFactory;
    private readonly TimeProvider _time;
    private readonly HoldWindow _activity = new(TimeSpan.FromSeconds(5));
    private readonly bool _ownsClient;

    private readonly Dictionary<string, SyncthingFolderStatus> _folders = new(StringComparer.Ordinal);

    private long _lastEventId = -1;
    private bool _reseedFolders = true;

    public SyncthingSyncProvider(
        Func<SyncthingEndpoint?>? endpointFactory = null,
        HttpClient? httpClient = null,
        TimeProvider? timeProvider = null)
    {
        _endpointFactory = endpointFactory ?? (() => SyncthingConfigReader.TryRead());
        _time = timeProvider ?? TimeProvider.System;
        _ownsClient = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
    }

    public string ProviderId => "syncthing";

    public char Initial => 'S';

    public async Task<SyncProviderSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var endpoint = _endpointFactory();
        if (endpoint is null)
        {
            return SyncProviderSnapshot.Absent(ProviderId, Initial);
        }

        if (!await IsHealthyAsync(endpoint, cancellationToken).ConfigureAwait(false))
        {
            return SyncProviderSnapshot.Absent(ProviderId, Initial);
        }

        try
        {
            var payload = await GetAsync<CompletionPayload>(endpoint, "/rest/db/completion", cancellationToken)
                .ConfigureAwait(false);

            if (payload is null)
            {
                return new SyncProviderSnapshot(ProviderId, Initial, SyncState.Unknown, "empty API response");
            }

            var localCompletion = new SyncthingCompletion(
                payload.Completion, payload.NeedBytes, payload.NeedItems, payload.NeedDeletes);

            var remotes = await GetRemoteCompletionsAsync(endpoint, cancellationToken).ConfigureAwait(false);
            var counterState = SyncthingStatusMapper.Aggregate(localCompletion, remotes);

            var now = _time.GetUtcNow();
            if (await PollEventsAsync(endpoint, cancellationToken).ConfigureAwait(false))
            {
                _activity.Mark(now);
            }

            var folders = await ReadFolderStatusesAsync(endpoint, cancellationToken).ConfigureAwait(false);
            var folderState = SyncthingStatusMapper.AggregateFolders(folders);

            // A small edit finishes between polls, so recent events are what reveal it.
            var recentlyActive = _activity.IsActive(now);
            var settled = SyncthingStatusMapper.Worse(counterState, folderState);
            var state = settled == SyncState.UpToDate && recentlyActive ? SyncState.Synchronizing : settled;

            var detail = folderState != SyncState.UpToDate
                ? SyncthingStatusMapper.DescribeFolders(folders)
                : counterState != SyncState.UpToDate
                    ? SyncthingStatusMapper.Describe(localCompletion, remotes)
                    : recentlyActive
                        ? "transferring"
                        : "all folders up to date";

            return new SyncProviderSnapshot(ProviderId, Initial, state, detail);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // The API key must never reach a message surfaced to the user.
            return new SyncProviderSnapshot(ProviderId, Initial, SyncState.Unknown, "API unreachable");
        }
    }

    /// <summary>
    /// Only connected peers count. A device that is offline may legitimately be behind, and treating that as an
    /// in-progress sync would leave the indicator red indefinitely.
    /// </summary>
    private async Task<List<SyncthingCompletion>> GetRemoteCompletionsAsync(
        SyncthingEndpoint endpoint, CancellationToken cancellationToken)
    {
        var results = new List<SyncthingCompletion>();

        try
        {
            var connections = await GetAsync<ConnectionsPayload>(endpoint, "/rest/system/connections", cancellationToken)
                .ConfigureAwait(false);

            var connected = connections?.Connections?
                .Where(pair => pair.Value.Connected)
                .Select(pair => pair.Key)
                .ToList() ?? [];

            foreach (var device in connected)
            {
                var path = $"/rest/db/completion?device={Uri.EscapeDataString(device)}";
                var remote = await GetAsync<CompletionPayload>(endpoint, path, cancellationToken).ConfigureAwait(false);
                if (remote is null)
                {
                    continue;
                }

                results.Add(new SyncthingCompletion(
                    remote.Completion, remote.NeedBytes, remote.NeedItems, remote.NeedDeletes));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Peers we cannot query simply do not contribute to the decision.
        }

        return results;
    }

    /// <summary>
    /// Syncthing buffers events, so asking for everything since the last seen id catches bursts that started and
    /// finished entirely between two polls. The first call only establishes the starting position.
    /// </summary>
    private async Task<bool> PollEventsAsync(SyncthingEndpoint endpoint, CancellationToken cancellationToken)
    {
        try
        {
            var priming = _lastEventId < 0;
            var since = priming ? 0 : _lastEventId;
            var limit = priming ? 1 : 200;
            var path = $"/rest/events?since={since}&limit={limit}&timeout=0";

            var events = await GetAsync<List<EventPayload>>(endpoint, path, cancellationToken).ConfigureAwait(false);
            if (events is null || events.Count == 0)
            {
                return false;
            }

            _lastEventId = events.Max(item => item.Id);
            if (priming)
            {
                return false;
            }

            // A full page means older events may already have scrolled past, so the cached states are unreliable.
            if (events.Count >= limit)
            {
                _reseedFolders = true;
            }

            var active = false;
            foreach (var item in events)
            {
                ApplyFolderEvent(item);
                active |= SyncthingEventFilter.IndicatesActivity(item.Type, StateTarget(item));
            }

            return active;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // A failed event query only costs us this round of activity detection.
            return false;
        }
    }

    /// <summary>Folder state is kept current from the event stream, which is cheap and already being polled.</summary>
    private void ApplyFolderEvent(EventPayload item)
    {
        if (item.Data.ValueKind != JsonValueKind.Object ||
            !item.Data.TryGetProperty("folder", out var folder) ||
            folder.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var folderId = folder.GetString();
        if (string.IsNullOrEmpty(folderId) || !_folders.TryGetValue(folderId, out var known))
        {
            return;
        }

        switch (item.Type)
        {
            case "StateChanged" when StateTarget(item) is { Length: > 0 } target:
                _folders[folderId] = known with { State = target };
                break;

            case "FolderSummary" when item.Data.TryGetProperty("summary", out var summary):
                var updated = summary.Deserialize<FolderStatusPayload>();
                if (updated is not null)
                {
                    _folders[folderId] = updated.ToStatus(known.Name);
                }

                break;
        }
    }

    /// <summary>
    /// The documented-expensive db/status call is used only to seed a folder we have not seen yet, or to recover
    /// after an event page came back full and may have dropped a transition. Steady state costs nothing extra.
    /// </summary>
    private async Task<List<SyncthingFolderStatus>> ReadFolderStatusesAsync(
        SyncthingEndpoint endpoint, CancellationToken cancellationToken)
    {
        var statuses = new List<SyncthingFolderStatus>();

        try
        {
            var configs = await GetAsync<List<FolderConfigPayload>>(endpoint, "/rest/config/folders", cancellationToken)
                .ConfigureAwait(false) ?? [];

            var unresolved = false;
            foreach (var config in configs)
            {
                if (config.Id is not { Length: > 0 } id)
                {
                    continue;
                }

                var name = string.IsNullOrWhiteSpace(config.Label) ? id : config.Label;

                // A paused folder reports no progress at all, so its config flag is the only signal available.
                if (config.Paused)
                {
                    statuses.Add(new SyncthingFolderStatus(name, SyncthingFolderStatus.PausedState, 0, 0, 0));
                    continue;
                }

                if (!_reseedFolders && _folders.TryGetValue(id, out var known))
                {
                    statuses.Add(known with { Name = name });
                    _folders[id] = known with { Name = name };
                    continue;
                }

                var path = $"/rest/db/status?folder={Uri.EscapeDataString(id)}";
                var payload = await GetAsync<FolderStatusPayload>(endpoint, path, cancellationToken).ConfigureAwait(false);
                if (payload is null)
                {
                    unresolved = true;
                    continue;
                }

                var seeded = payload.ToStatus(name);
                _folders[id] = seeded;
                statuses.Add(seeded);
            }

            _reseedFolders = unresolved;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Folder detail is an enrichment; the completion counters still drive the circle without it.
            _reseedFolders = true;
        }

        return statuses;
    }

    private static string? StateTarget(EventPayload item)
    {
        if (item.Data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return item.Data.TryGetProperty("to", out var to) && to.ValueKind == JsonValueKind.String
            ? to.GetString()
            : null;
    }

    private async Task<T?> GetAsync<T>(SyncthingEndpoint endpoint, string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint.BaseAddress, path));
        request.Headers.TryAddWithoutValidation("X-API-Key", endpoint.ApiKey);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsHealthyAsync(SyncthingEndpoint endpoint, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http
                .GetAsync(new Uri(endpoint.BaseAddress, "/rest/noauth/health"), cancellationToken)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }

    private sealed record CompletionPayload
    {
        [JsonPropertyName("completion")]
        public double Completion { get; init; }

        [JsonPropertyName("needBytes")]
        public long NeedBytes { get; init; }

        [JsonPropertyName("needItems")]
        public long NeedItems { get; init; }

        [JsonPropertyName("needDeletes")]
        public long NeedDeletes { get; init; }
    }

    private sealed record ConnectionsPayload
    {
        [JsonPropertyName("connections")]
        public Dictionary<string, ConnectionEntry>? Connections { get; init; }
    }

    private sealed record ConnectionEntry
    {
        [JsonPropertyName("connected")]
        public bool Connected { get; init; }
    }

    private sealed record EventPayload
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }

        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("data")]
        public JsonElement Data { get; init; }
    }

    private sealed record FolderConfigPayload
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("label")]
        public string? Label { get; init; }

        [JsonPropertyName("paused")]
        public bool Paused { get; init; }
    }

    private sealed record FolderStatusPayload
    {
        [JsonPropertyName("state")]
        public string? State { get; init; }

        [JsonPropertyName("needTotalItems")]
        public long NeedTotalItems { get; init; }

        [JsonPropertyName("pullErrors")]
        public long PullErrors { get; init; }

        [JsonPropertyName("receiveOnlyChangedFiles")]
        public long ReceiveOnlyChangedFiles { get; init; }

        internal SyncthingFolderStatus ToStatus(string name) =>
            new(name, State ?? string.Empty, NeedTotalItems, PullErrors, ReceiveOnlyChangedFiles);
    }
}
