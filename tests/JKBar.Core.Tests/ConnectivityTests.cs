// Pins the connectivity rules: every probe feeds the verdict, and only changes are announced.
using JKBar.Core.Alerts;
using JKBar.Core.Network;
using JKBar.Core.Settings;

namespace JKBar.Core.Tests;

public class ConnectivityTests
{
    private static readonly ConnectivityReport Online = new(ConnectivityState.Online, []);
    private static readonly ConnectivityReport Offline = new(ConnectivityState.Offline, []);

    private static ConnectivityReport Partial(params string[] unreachable) =>
        new(ConnectivityState.Partial, unreachable);

    [Theory]
    [InlineData(true, true, true, ConnectivityState.Online)]
    [InlineData(true, false, true, ConnectivityState.Partial)]
    [InlineData(false, false, true, ConnectivityState.Partial)]
    [InlineData(false, false, false, ConnectivityState.Offline)]
    public void CombinesEveryProbeIntoOneVerdict(bool first, bool second, bool third, ConnectivityState expected)
    {
        var report = ConnectivityVerdict.From(
        [
            new ProbeResult("a.example", first),
            new ProbeResult("b.example", second),
            new ProbeResult("c.example", third)
        ]);

        Assert.Equal(expected, report.State);
    }

    [Fact]
    public void APartialVerdictNamesTheSitesThatDidNotAnswerInOrder()
    {
        var report = ConnectivityVerdict.From(
        [
            new ProbeResult("www.google.com", true),
            new ProbeResult("intranet.corp", false),
            new ProbeResult("www.msftconnecttest.com", false)
        ]);

        Assert.Equal(["intranet.corp", "www.msftconnecttest.com"], report.Unreachable);
    }

    [Fact]
    public void NoProbesIsUnknown() =>
        Assert.Equal(ConnectivityState.Unknown, ConnectivityVerdict.From([]).State);

    [Theory]
    [InlineData("https://www.google.com/generate_204", 204, true)]
    [InlineData("https://www.google.com/generate_204", 200, false)]
    [InlineData("http://www.msftconnecttest.com/connecttest.txt", 200, true)]
    [InlineData("https://intranet.corp/health", 204, true)]
    [InlineData("https://intranet.corp/health", 302, false)]
    [InlineData("https://intranet.corp/health", 503, false)]
    public void OnlyADirectSuccessCounts(string probe, int status, bool expected) =>
        Assert.Equal(expected, ConnectivityVerdict.IsSuccess(new Uri(probe), status));

    [Fact]
    public void DescribesFailingSitesBrieflyForTheNotch()
    {
        Assert.Equal("일부 사이트 접속 가능 (a.example 안 됨)", ConnectivityVerdict.DescribeUnreachable(["a.example"]));
        Assert.Equal(
            "일부 사이트 접속 가능 (a.example, b.example 외 2곳 안 됨)",
            ConnectivityVerdict.DescribeUnreachable(["a.example", "b.example", "c.example", "d.example"]));
    }

    [Fact]
    public void StaysSilentWhenTheFirstReadingIsHealthy()
    {
        var watcher = new ConnectivityWatcher();

        Assert.Null(watcher.Observe(Online));
    }

    [Fact]
    public void AnnouncesAFirstReadingThatIsAlreadyBroken()
    {
        var watcher = new ConnectivityWatcher();

        var alert = watcher.Observe(Offline);

        Assert.Equal("network.offline", alert?.Key);
        Assert.Equal("네트워크 연결 끊김", alert?.Title);
        Assert.Equal(AlertSeverity.Warning, alert?.Severity);
    }

    [Fact]
    public void StaysSilentWhileTheVerdictHolds()
    {
        var watcher = new ConnectivityWatcher();
        watcher.Observe(Offline);

        Assert.Null(watcher.Observe(Offline));
    }

    [Fact]
    public void AnnouncesAPartialOutageOnceWithTheFailingSite()
    {
        var watcher = new ConnectivityWatcher();
        watcher.Observe(Online);

        var alert = watcher.Observe(Partial("intranet.corp"));

        Assert.Equal("네트워크 연결됨", alert?.Title);
        Assert.Equal("일부 사이트 접속 가능 (intranet.corp 안 됨)", alert?.Detail);
        Assert.Null(watcher.Observe(Partial("intranet.corp")));
    }

    [Fact]
    public void AnnouncesAgainWhenADifferentSiteFails()
    {
        var watcher = new ConnectivityWatcher();
        watcher.Observe(Partial("a.example"));

        var alert = watcher.Observe(Partial("b.example"));

        Assert.Equal("일부 사이트 접속 가능 (b.example 안 됨)", alert?.Detail);
    }

    [Fact]
    public void ReportsRecoveryFromAPartialOutageOnce()
    {
        var watcher = new ConnectivityWatcher();
        watcher.Observe(Partial("a.example"));

        var alert = watcher.Observe(Online);

        Assert.Equal("network.online", alert?.Key);
        Assert.Equal("네트워크 연결됨", alert?.Title);
        Assert.Equal("모든 사이트 접속 가능", alert?.Detail);
        Assert.Equal(AlertSeverity.Done, alert?.Severity);
        Assert.Null(watcher.Observe(Online));
    }

    [Fact]
    public void ReportsRecoveryAsDone()
    {
        var watcher = new ConnectivityWatcher();
        watcher.Observe(Offline);

        var alert = watcher.Observe(Online);

        Assert.Equal("network.online", alert?.Key);
        Assert.Equal(AlertSeverity.Done, alert?.Severity);
    }

    [Fact]
    public void IgnoresAnUnknownReadingSoAFailedCheckDoesNotLookLikeRecovery()
    {
        var watcher = new ConnectivityWatcher();
        watcher.Observe(Offline);

        Assert.Null(watcher.Observe(ConnectivityReport.Unknown));
        Assert.Equal(ConnectivityState.Offline, watcher.State);
    }

    [Fact]
    public void DefaultsCheckTheTwoStandardEndpointsEveryThirtySeconds()
    {
        var settings = new ConnectivitySettings().Normalized();

        Assert.Equal(ConnectivitySettings.DefaultProbeUrls, settings.ProbeUrls);
        Assert.Equal(30, settings.IntervalSeconds);
    }

    [Theory]
    [InlineData(5, 10)]
    [InlineData(45, 45)]
    [InlineData(120, 60)]
    public void KeepsTheIntervalBetweenTenAndSixtySeconds(int requested, int expected) =>
        Assert.Equal(expected, new ConnectivitySettings { IntervalSeconds = requested }.Normalized().IntervalSeconds);

    [Fact]
    public void KeepsAtMostFiveValidDistinctAddresses()
    {
        var settings = new ConnectivitySettings
        {
            ProbeUrls =
            [
                " https://a.example/ ", "ftp://b.example", "not a url", "https://A.example/",
                "https://c.example", "https://d.example", "https://e.example", "https://f.example", "https://g.example"
            ]
        }.Normalized();

        Assert.Equal(
            ["https://a.example/", "https://c.example", "https://d.example", "https://e.example", "https://f.example"],
            settings.ProbeUrls);
    }

    [Fact]
    public void FallsBackToTheDefaultsWhenNoAddressIsUsable() =>
        Assert.Equal(
            ConnectivitySettings.DefaultProbeUrls,
            new ConnectivitySettings { ProbeUrls = ["", "mailto:x@example.com"] }.Normalized().ProbeUrls);

    [Fact]
    public void OldSettingsWithoutTheSectionGetTheDefaults()
    {
        var settings = new JkBarSettings { Connectivity = null! }.Normalized();

        Assert.Equal(ConnectivitySettings.DefaultProbeUrls, settings.Connectivity.ProbeUrls);
        Assert.Equal(30, settings.Connectivity.IntervalSeconds);
    }
}
