// Ported from JKMon (packages/JKMon/src/JKMon.Core/Sync). Since 0.8.8 JKBar no longer polls db/completion; JKMon still does.
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKBar.Core.Sync;

/// <summary>
/// Follows the local Syncthing REST event stream. db/completion is never polled: on a large index every call walks
/// the whole need set in SQLite and a 5 s cadence kept the daemon busy on several cores while fully idle.
/// </summary>
public sealed class SyncthingSyncProvider : ISyncProvider, IDisposable
{
    internal const string EventMask =
        "StateChanged,FolderSummary,FolderCompletion,DeviceConnected,DeviceDisconnected,ConfigSaved," +
        "ItemStarted,ItemFinished,LocalIndexUpdated,RemoteIndexUpdated,DownloadProgress,RemoteDownloadProgress," +
        "FolderScanProgress";

    internal const int EventPageLimit = 200;

    internal static readonly TimeSpan ReconcileInterval = TimeSpan.FromMinutes(5);
    internal static readonly TimeSpan FolderConfigInterval = TimeSpan.FromSeconds(60);
    internal static readonly TimeSpan FolderReseedInterval = TimeSpan.FromSeconds(60);
    internal static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    private static readonly SyncthingCompletion NothingNeeded = new(100, 0, 0, 0);

    private readonly HttpClient _http;
    private readonly Func<SyncthingEndpoint?> _endpointFactory;
    private readonly TimeProvider _time;
    private readonly HoldWindow _activity = new(TimeSpan.FromSeconds(5));
    private readonly bool _ownsClient;

    private readonly Dictionary<string, SyncthingFolderStatus> _folders = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Device, string Folder), SyncthingCompletion> _remotes = [];

    private HashSet<string>? _connected;
    private DateTimeOffset _reconciledAt;
    private List<FolderConfigPayload>? _folderConfigs;
    private DateTimeOffset _folderConfigsReadAt;
    private bool _reloadFolderConfigs;
    private long _lastEventId = -1;
    private bool _reseedFolders = true;
    private DateTimeOffset _reseededAt = DateTimeOffset.MinValue;
    private int _failures;
    private DateTimeOffset _retryAt = DateTimeOffset.MinValue;

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
            // A restarted daemon numbers its events from scratch, so nothing cached survives an outage.
            ResetState();
            return SyncProviderSnapshot.Absent(ProviderId, Initial);
        }

        var now = _time.GetUtcNow();
        if (now < _retryAt)
        {
            return Unreachable();
        }

        try
        {
            await ReconcileAsync(endpoint, now, cancellationToken).ConfigureAwait(false);

            if (await PollEventsAsync(endpoint, cancellationToken).ConfigureAwait(false))
            {
                _activity.Mark(now);
            }

            var folders = await ReadFolderStatusesAsync(endpoint, now, cancellationToken).ConfigureAwait(false);
            _failures = 0;
            _retryAt = DateTimeOffset.MinValue;

            var folderState = SyncthingStatusMapper.AggregateFolders(folders);
            var remotes = ConnectedRemotes();
            var counterState = SyncthingStatusMapper.Aggregate(NothingNeeded, remotes);

            // A small edit finishes between polls, so recent events are what reveal it.
            var recentlyActive = _activity.IsActive(now);
            var settled = SyncthingStatusMapper.Worse(counterState, folderState);
            var state = settled == SyncState.UpToDate && recentlyActive ? SyncState.Synchronizing : settled;

            var detail = folderState != SyncState.UpToDate
                ? SyncthingStatusMapper.DescribeFolders(folders)
                : counterState != SyncState.UpToDate
                    ? SyncthingStatusMapper.Describe(NothingNeeded, remotes)
                    : recentlyActive
                        ? "transferring"
                        : "all folders up to date";

            return new SyncProviderSnapshot(ProviderId, Initial, state, detail);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // A struggling daemon is left alone for a while instead of being asked again on the next tick.
            _failures++;
            var delay = _failures <= 1
                ? TimeSpan.Zero
                : TimeSpan.FromTicks(Math.Min(
                    InitialBackoff.Ticks * (1L << Math.Min(_failures - 2, 8)), MaxBackoff.Ticks));
            _retryAt = now + delay;
            ResetState();
            return Unreachable();
        }
    }

    // The API key must never reach a message surfaced to the user.
    private SyncProviderSnapshot Unreachable() =>
        new(ProviderId, Initial, SyncState.Unknown, "API unreachable");

    private void ResetState()
    {
        _folders.Clear();
        _remotes.Clear();
        _connected = null;
        _folderConfigs = null;
        _lastEventId = -1;
        _reseedFolders = true;
        _reseededAt = DateTimeOffset.MinValue;
    }

    /// <summary>
    /// Only connected peers count. A device that is offline may legitimately be behind, and treating that as an
    /// in-progress sync would leave the indicator red indefinitely.
    /// </summary>
    private List<SyncthingCompletion> ConnectedRemotes()
    {
        if (_connected is null)
        {
            return [];
        }

        return _remotes
            .Where(pair => _connected.Contains(pair.Key.Device))
            .GroupBy(pair => pair.Key.Device, StringComparer.Ordinal)
            .Select(group => new SyncthingCompletion(
                group.Min(pair => pair.Value.Completion),
                group.Sum(pair => pair.Value.NeedBytes),
                group.Sum(pair => pair.Value.NeedItems),
                group.Sum(pair => pair.Value.NeedDeletes)))
            .ToList();
    }

    /// <summary>
    /// Connection changes arrive as events; the list is re-read only at start and every few minutes as a safety net,
    /// together with a check that the daemon has not restarted and reset its event ids between two polls.
    /// </summary>
    private async Task ReconcileAsync(SyncthingEndpoint endpoint, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (_connected is not null && now - _reconciledAt < ReconcileInterval)
        {
            return;
        }

        if (_lastEventId > 0)
        {
            var latest = await GetAsync<List<EventPayload>>(endpoint, EventsPath(0, 1), cancellationToken)
                .ConfigureAwait(false);
            if (latest is not { Count: > 0 } || latest.Max(item => item.Id) < _lastEventId)
            {
                ResetState();
            }
        }

        var connections = await GetAsync<ConnectionsPayload>(endpoint, "/rest/system/connections", cancellationToken)
            .ConfigureAwait(false);

        _connected = new HashSet<string>(
            connections?.Connections?.Where(pair => pair.Value.Connected).Select(pair => pair.Key) ?? [],
            StringComparer.Ordinal);
        _reconciledAt = now;
    }

    private static string EventsPath(long since, int limit) =>
        $"/rest/events?events={EventMask}&since={since}&limit={limit}&timeout=0";

    /// <summary>
    /// Syncthing buffers events, so asking for everything since the last seen id catches bursts that started and
    /// finished entirely between two polls. The first call only establishes the starting position.
    /// </summary>
    private async Task<bool> PollEventsAsync(SyncthingEndpoint endpoint, CancellationToken cancellationToken)
    {
        var priming = _lastEventId < 0;
        var since = priming ? 0 : _lastEventId;
        var limit = priming ? 1 : EventPageLimit;

        var events = await GetAsync<List<EventPayload>>(endpoint, EventsPath(since, limit), cancellationToken)
            .ConfigureAwait(false);
        if (events is null || events.Count == 0)
        {
            // The masked subscription is created by this first request, so everything after it is still to come.
            if (priming)
            {
                _lastEventId = 0;
            }

            return false;
        }

        _lastEventId = events.Max(item => item.Id);
        if (priming)
        {
            return false;
        }

        // A full page means older events may already have scrolled past, so the cached states are unreliable.
        // A stale peer entry would hold the indicator in sync forever, so peers restart from the next report.
        if (events.Count >= limit)
        {
            _reseedFolders = true;
            _remotes.Clear();
        }

        var active = false;
        foreach (var item in events)
        {
            ApplyEvent(item);
            active |= SyncthingEventFilter.IndicatesActivity(item.Type, StateTarget(item));
        }

        return active;
    }

    /// <summary>Folder, peer and connection state are kept current from the event stream, which is cheap.</summary>
    private void ApplyEvent(EventPayload item)
    {
        if (item.Data.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        switch (item.Type)
        {
            case "ConfigSaved":
                _reloadFolderConfigs = true;
                return;

            case "DeviceConnected" when StringProperty(item.Data, "id") is { Length: > 0 } device:
                _connected?.Add(device);
                return;

            case "DeviceDisconnected" when StringProperty(item.Data, "id") is { Length: > 0 } device:
                _connected?.Remove(device);
                foreach (var key in _remotes.Keys.Where(key => key.Device == device).ToList())
                {
                    _remotes.Remove(key);
                }

                return;

            case "FolderCompletion":
                ApplyCompletion(item.Data);
                return;
        }

        if (StringProperty(item.Data, "folder") is not { Length: > 0 } folderId)
        {
            return;
        }

        ApplyFolderEvent(item, folderId);
    }

    private void ApplyCompletion(JsonElement data)
    {
        var payload = data.Deserialize<FolderCompletionPayload>();
        if (payload is not { Device: { Length: > 0 } device, Folder: { Length: > 0 } folder })
        {
            return;
        }

        // A peer that paused or stopped sharing the folder reports no progress and is not waiting on us.
        if (payload.RemoteState is { Length: > 0 } remoteState &&
            !string.Equals(remoteState, "valid", StringComparison.OrdinalIgnoreCase))
        {
            _remotes.Remove((device, folder));
            return;
        }

        _remotes[(device, folder)] = new SyncthingCompletion(
            payload.Completion, payload.NeedBytes, payload.NeedItems, payload.NeedDeletes);
    }

    private static string? StringProperty(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private void ApplyFolderEvent(EventPayload item, string folderId)
    {
        if (!_folders.TryGetValue(folderId, out var known))
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
    /// after an event page came back full, and that recovery is rate limited. Steady state costs nothing extra.
    /// </summary>
    private async Task<List<SyncthingFolderStatus>> ReadFolderStatusesAsync(
        SyncthingEndpoint endpoint, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (_folderConfigs is null || _reloadFolderConfigs || now - _folderConfigsReadAt >= FolderConfigInterval)
        {
            _folderConfigs = await GetAsync<List<FolderConfigPayload>>(endpoint, "/rest/config/folders", cancellationToken)
                .ConfigureAwait(false) ?? throw new JsonException("empty folder configuration");
            _folderConfigsReadAt = now;
            _reloadFolderConfigs = false;
        }

        var reseed = _reseedFolders && now - _reseededAt >= FolderReseedInterval;
        var statuses = new List<SyncthingFolderStatus>();
        var configured = new HashSet<string>(StringComparer.Ordinal);

        foreach (var config in _folderConfigs)
        {
            if (config.Id is not { Length: > 0 } id)
            {
                continue;
            }

            configured.Add(id);
            var name = string.IsNullOrWhiteSpace(config.Label) ? id : config.Label;

            // A paused folder reports no progress at all, so its config flag is the only signal available.
            if (config.Paused)
            {
                statuses.Add(new SyncthingFolderStatus(name, SyncthingFolderStatus.PausedState, 0, 0, 0));
                continue;
            }

            if (!reseed && _folders.TryGetValue(id, out var known))
            {
                statuses.Add(known with { Name = name });
                _folders[id] = known with { Name = name };
                continue;
            }

            var path = $"/rest/db/status?folder={Uri.EscapeDataString(id)}";
            var payload = await GetAsync<FolderStatusPayload>(endpoint, path, cancellationToken).ConfigureAwait(false)
                ?? throw new JsonException("empty folder status");

            var seeded = payload.ToStatus(name);
            _folders[id] = seeded;
            statuses.Add(seeded);
        }

        foreach (var removed in _folders.Keys.Where(id => !configured.Contains(id)).ToList())
        {
            _folders.Remove(removed);
        }

        foreach (var removed in _remotes.Keys.Where(key => !configured.Contains(key.Folder)).ToList())
        {
            _remotes.Remove(removed);
        }

        if (reseed)
        {
            _reseedFolders = false;
            _reseededAt = now;
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
            throw new HttpRequestException($"Syncthing API returned {(int)response.StatusCode}", null, response.StatusCode);
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

    private sealed record FolderCompletionPayload
    {
        [JsonPropertyName("device")]
        public string? Device { get; init; }

        [JsonPropertyName("folder")]
        public string? Folder { get; init; }

        [JsonPropertyName("remoteState")]
        public string? RemoteState { get; init; }

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
