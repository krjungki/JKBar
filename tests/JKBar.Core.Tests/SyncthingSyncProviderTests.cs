// Drives SyncthingSyncProvider against an in-memory REST fake so every endpoint call can be counted.
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using JKBar.Core.Sync;

namespace JKBar.Core.Tests;

public class SyncthingSyncProviderTests
{
    private const string ApiKey = "test-api-key-7f3a";
    private const string Peer = "PEER-DEVICE";

    [Fact]
    public async Task SteadyStateNeverAsksForCompletion()
    {
        var (daemon, clock, provider) = Create();

        for (var tick = 0; tick < 12; tick++)
        {
            var snapshot = await provider.GetSnapshotAsync(CancellationToken.None);
            Assert.Equal(SyncState.UpToDate, snapshot.State);
            clock.Advance(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(0, daemon.Count("/rest/db/completion"));
        Assert.Equal(1, daemon.Count("/rest/system/connections"));
        Assert.Equal(1, daemon.Count("/rest/db/status"));
        Assert.Equal(1, daemon.Count("/rest/config/folders"));
        Assert.All(daemon.Calls.Where(call => call.StartsWith("/rest/events")),
            call => Assert.Contains("events=", call));
    }

    [Fact]
    public async Task FolderEventsDriveLocalState()
    {
        var (daemon, clock, provider) = Create();
        await provider.GetSnapshotAsync(CancellationToken.None);

        daemon.Emit("FolderSummary", new { folder = "docs", summary = new { state = "syncing", needTotalItems = 4 } });
        clock.Advance(TimeSpan.FromSeconds(5));
        var syncing = await provider.GetSnapshotAsync(CancellationToken.None);
        Assert.Equal(SyncState.Synchronizing, syncing.State);
        Assert.Contains("Docs", syncing.Detail);

        daemon.Emit("FolderSummary", new { folder = "docs", summary = new { state = "idle", needTotalItems = 0 } });
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(SyncState.UpToDate, (await provider.GetSnapshotAsync(CancellationToken.None)).State);

        daemon.Emit("FolderSummary", new { folder = "docs", summary = new { state = "idle", pullErrors = 2 } });
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(SyncState.Error, (await provider.GetSnapshotAsync(CancellationToken.None)).State);
        Assert.Equal(1, daemon.Count("/rest/db/status"));
    }

    [Fact]
    public async Task ConnectedPeerBehindShowsSendingUntilItDisconnects()
    {
        var (daemon, clock, provider) = Create();
        await provider.GetSnapshotAsync(CancellationToken.None);

        daemon.Emit("FolderCompletion", new { device = Peer, folder = "docs", completion = 40.0, needItems = 3, remoteState = "valid" });
        clock.Advance(TimeSpan.FromSeconds(5));
        var sending = await provider.GetSnapshotAsync(CancellationToken.None);
        Assert.Equal(SyncState.Synchronizing, sending.State);
        Assert.Equal("sending 3 item(s) to 1 device(s)", sending.Detail);

        daemon.Emit("DeviceDisconnected", new { id = Peer });
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(SyncState.UpToDate, (await provider.GetSnapshotAsync(CancellationToken.None)).State);

        daemon.Emit("FolderCompletion", new { device = Peer, folder = "docs", completion = 40.0, needItems = 3 });
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(SyncState.UpToDate, (await provider.GetSnapshotAsync(CancellationToken.None)).State);

        daemon.Emit("DeviceConnected", new { id = Peer });
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(SyncState.Synchronizing, (await provider.GetSnapshotAsync(CancellationToken.None)).State);

        daemon.Emit("FolderCompletion", new { device = Peer, folder = "docs", completion = 100.0 });
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(SyncState.UpToDate, (await provider.GetSnapshotAsync(CancellationToken.None)).State);
        Assert.Equal(0, daemon.Count("/rest/db/completion"));
    }

    [Fact]
    public async Task PeerThatPausedTheFolderIsNotWaitingOnUs()
    {
        var (daemon, clock, provider) = Create();
        await provider.GetSnapshotAsync(CancellationToken.None);

        daemon.Emit("FolderCompletion", new { device = Peer, folder = "docs", completion = 0.0, needItems = 9, remoteState = "paused" });
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(SyncState.UpToDate, (await provider.GetSnapshotAsync(CancellationToken.None)).State);
    }

    [Fact]
    public async Task FullEventPageReseedsOnceWithinTheInterval()
    {
        var (daemon, clock, provider) = Create();
        await provider.GetSnapshotAsync(CancellationToken.None);
        clock.Advance(SyncthingSyncProvider.FolderReseedInterval);

        daemon.EmitMany(SyncthingSyncProvider.EventPageLimit + 10);
        clock.Advance(TimeSpan.FromSeconds(5));
        await provider.GetSnapshotAsync(CancellationToken.None);
        Assert.Equal(2, daemon.Count("/rest/db/status"));

        daemon.EmitMany(SyncthingSyncProvider.EventPageLimit + 10);
        clock.Advance(TimeSpan.FromSeconds(5));
        await provider.GetSnapshotAsync(CancellationToken.None);
        Assert.Equal(2, daemon.Count("/rest/db/status"));

        clock.Advance(SyncthingSyncProvider.FolderReseedInterval);
        await provider.GetSnapshotAsync(CancellationToken.None);
        Assert.Equal(3, daemon.Count("/rest/db/status"));
    }

    [Fact]
    public async Task RepeatedFailuresBackOffWithoutQueryingTheDaemon()
    {
        var (daemon, clock, provider) = Create();
        await provider.GetSnapshotAsync(CancellationToken.None);

        daemon.Failing = true;
        for (var failure = 0; failure < 2; failure++)
        {
            clock.Advance(TimeSpan.FromSeconds(5));
            var snapshot = await provider.GetSnapshotAsync(CancellationToken.None);
            Assert.Equal(SyncState.Unknown, snapshot.State);
            Assert.DoesNotContain(ApiKey, snapshot.Detail);
        }

        var authedCalls = daemon.AuthedCount;
        for (var tick = 0; tick < 5; tick++)
        {
            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.Equal(SyncState.Unknown, (await provider.GetSnapshotAsync(CancellationToken.None)).State);
        }

        Assert.Equal(authedCalls, daemon.AuthedCount);

        daemon.Failing = false;
        clock.Advance(SyncthingSyncProvider.InitialBackoff);
        Assert.Equal(SyncState.UpToDate, (await provider.GetSnapshotAsync(CancellationToken.None)).State);
        Assert.True(daemon.AuthedCount > authedCalls);
    }

    [Fact]
    public async Task RestartedDaemonIsDetectedAtReconcile()
    {
        var (daemon, clock, provider) = Create();
        await provider.GetSnapshotAsync(CancellationToken.None);
        daemon.EmitMany(20);
        clock.Advance(TimeSpan.FromSeconds(5));
        await provider.GetSnapshotAsync(CancellationToken.None);

        daemon.Restart();
        clock.Advance(SyncthingSyncProvider.ReconcileInterval);
        await provider.GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(2, daemon.Count("/rest/db/status"));
        Assert.Equal(2, daemon.Count("/rest/system/connections"));

        daemon.Emit("FolderSummary", new { folder = "docs", summary = new { state = "syncing", needTotalItems = 1 } });
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(SyncState.Synchronizing, (await provider.GetSnapshotAsync(CancellationToken.None)).State);
    }

    [Fact]
    public async Task UnhealthyDaemonIsAbsentAndPausedFolderIsNotSettled()
    {
        var (daemon, _, provider) = Create();
        daemon.Healthy = false;
        Assert.Equal(SyncState.Absent, (await provider.GetSnapshotAsync(CancellationToken.None)).State);

        daemon.Healthy = true;
        daemon.PausedFolder = true;
        var snapshot = await provider.GetSnapshotAsync(CancellationToken.None);
        Assert.Equal(SyncState.Synchronizing, snapshot.State);
        Assert.Equal(0, daemon.Count("/rest/db/status"));
    }

    [Fact]
    public async Task ConfigSavedReloadsFolderConfiguration()
    {
        var (daemon, clock, provider) = Create();
        await provider.GetSnapshotAsync(CancellationToken.None);

        daemon.PausedFolder = true;
        daemon.Emit("ConfigSaved", new { version = 2 });
        clock.Advance(TimeSpan.FromSeconds(5));
        await provider.GetSnapshotAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(5));
        var snapshot = await provider.GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(SyncState.Synchronizing, snapshot.State);
        Assert.Contains("paused", snapshot.Detail);
    }

    [Fact]
    public async Task RemovedFolderNoLongerHoldsPeerProgress()
    {
        var (daemon, clock, provider) = Create();
        await provider.GetSnapshotAsync(CancellationToken.None);

        daemon.Emit("FolderCompletion", new { device = Peer, folder = "docs", completion = 50.0, needItems = 5 });
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(SyncState.Synchronizing, (await provider.GetSnapshotAsync(CancellationToken.None)).State);

        daemon.FolderRemoved = true;
        daemon.Emit("ConfigSaved", new { version = 2 });
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(SyncState.UpToDate, (await provider.GetSnapshotAsync(CancellationToken.None)).State);
    }

    private static (FakeSyncthing Daemon, ManualClock Clock, SyncthingSyncProvider Provider) Create()
    {
        var daemon = new FakeSyncthing();
        var clock = new ManualClock();
        var endpoint = new SyncthingEndpoint { BaseAddress = new Uri("http://127.0.0.1:8384"), ApiKey = ApiKey };
        var provider = new SyncthingSyncProvider(() => endpoint, new HttpClient(daemon), clock);
        return (daemon, clock, provider);
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class FakeSyncthing : HttpMessageHandler
    {
        private readonly List<(long Id, string Type, object Data)> _events = [];
        private long _nextId = 1;

        public List<string> Calls { get; } = [];

        public int AuthedCount { get; private set; }

        public bool Healthy { get; set; } = true;

        public bool Failing { get; set; }

        public bool PausedFolder { get; set; }

        public bool FolderRemoved { get; set; }

        public int Count(string path) => Calls.Count(call => call.Split('?')[0] == path);

        public void Emit(string type, object data) => _events.Add((_nextId++, type, data));

        public void EmitMany(int count)
        {
            for (var i = 0; i < count; i++)
            {
                Emit("ItemFinished", new { folder = "docs", item = "file" });
            }
        }

        public void Restart()
        {
            _events.Clear();
            _nextId = 1;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            Calls.Add(uri.PathAndQuery);

            if (uri.AbsolutePath == "/rest/noauth/health")
            {
                return Task.FromResult(new HttpResponseMessage(Healthy ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable));
            }

            AuthedCount++;
            Assert.True(request.Headers.TryGetValues("X-API-Key", out var keys) && keys.Single() == ApiKey);
            if (Failing)
            {
                throw new HttpRequestException("connection refused");
            }

            object body = uri.AbsolutePath switch
            {
                "/rest/system/connections" => new { connections = new Dictionary<string, object> { [Peer] = new { connected = true } } },
                "/rest/config/folders" => FolderRemoved
                    ? Array.Empty<object>()
                    : new[] { new { id = "docs", label = "Docs", paused = PausedFolder } },
                "/rest/db/status" => new { state = "idle", needTotalItems = 0 },
                "/rest/events" => Events(HttpUtility.ParseQueryString(uri.Query)),
                _ => throw new InvalidOperationException($"unexpected endpoint {uri.AbsolutePath}")
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            });
        }

        private object Events(System.Collections.Specialized.NameValueCollection query)
        {
            var since = long.Parse(query["since"]!);
            var limit = int.Parse(query["limit"]!);
            var matching = _events.Where(item => item.Id > since).ToList();
            return matching.Skip(Math.Max(0, matching.Count - limit))
                .Select(item => new { id = item.Id, type = item.Type, data = item.Data })
                .ToList();
        }
    }
}
