// Pins the connectivity rules: two probes decide the verdict, and only changes are announced.
using JKBar.Core.Alerts;
using JKBar.Core.Network;

namespace JKBar.Core.Tests;

public class ConnectivityTests
{
    [Theory]
    [InlineData(true, true, ConnectivityState.Online)]
    [InlineData(true, false, ConnectivityState.Partial)]
    [InlineData(false, true, ConnectivityState.Partial)]
    [InlineData(false, false, ConnectivityState.Offline)]
    public void CombinesBothProbesIntoOneVerdict(bool google, bool microsoft, ConnectivityState expected) =>
        Assert.Equal(expected, ConnectivityVerdict.From(google, microsoft));

    [Fact]
    public void StaysSilentWhenTheFirstReadingIsHealthy()
    {
        var watcher = new ConnectivityWatcher();

        Assert.Null(watcher.Observe(ConnectivityState.Online));
    }

    [Fact]
    public void AnnouncesAFirstReadingThatIsAlreadyBroken()
    {
        var watcher = new ConnectivityWatcher();

        var alert = watcher.Observe(ConnectivityState.Offline);

        Assert.Equal("network.offline", alert?.Key);
        Assert.Equal(AlertSeverity.Warning, alert?.Severity);
    }

    [Fact]
    public void StaysSilentWhileTheVerdictHolds()
    {
        var watcher = new ConnectivityWatcher();
        watcher.Observe(ConnectivityState.Offline);

        Assert.Null(watcher.Observe(ConnectivityState.Offline));
    }

    [Fact]
    public void ReportsRecoveryAsDone()
    {
        var watcher = new ConnectivityWatcher();
        watcher.Observe(ConnectivityState.Offline);

        var alert = watcher.Observe(ConnectivityState.Online);

        Assert.Equal("network.online", alert?.Key);
        Assert.Equal(AlertSeverity.Done, alert?.Severity);
    }

    [Fact]
    public void IgnoresAnUnknownReadingSoAFailedCheckDoesNotLookLikeRecovery()
    {
        var watcher = new ConnectivityWatcher();
        watcher.Observe(ConnectivityState.Offline);

        Assert.Null(watcher.Observe(ConnectivityState.Unknown));
        Assert.Equal(ConnectivityState.Offline, watcher.State);
    }
}
