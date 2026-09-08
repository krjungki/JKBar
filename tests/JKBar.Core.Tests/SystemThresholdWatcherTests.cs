// A spike must stay silent; only a sustained reading is worth interrupting for.
using JKBar.Core.Alerts;
using JKBar.Core.Metrics;

namespace JKBar.Core.Tests;

public class SystemThresholdWatcherTests
{
    private static MetricsSnapshot Load(double cpu, double memory) => new(cpu, memory, 0, 0, 0, 0);

    [Fact]
    public void StaysSilentForABriefSpike()
    {
        var watcher = new SystemThresholdWatcher(percentThreshold: 90, sustainedSamples: 3);

        Assert.Empty(watcher.Observe(Load(95, 10)));
        Assert.Empty(watcher.Observe(Load(95, 10)));
        Assert.Empty(watcher.Observe(Load(20, 10)));
        Assert.Empty(watcher.Observe(Load(95, 10)));
    }

    [Fact]
    public void WarnsOnceTheReadingIsSustained()
    {
        var watcher = new SystemThresholdWatcher(percentThreshold: 90, sustainedSamples: 3);
        watcher.Observe(Load(95, 10));
        watcher.Observe(Load(95, 10));

        var alert = Assert.Single(watcher.Observe(Load(95, 10)));

        Assert.Equal("system.cpu", alert.Key);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
    }

    [Fact]
    public void DoesNotRepeatWhileTheReadingStaysHigh()
    {
        var watcher = new SystemThresholdWatcher(percentThreshold: 90, sustainedSamples: 2);
        watcher.Observe(Load(95, 10));
        watcher.Observe(Load(95, 10));

        Assert.Empty(watcher.Observe(Load(95, 10)));
    }

    [Fact]
    public void TracksCpuAndMemoryIndependently()
    {
        var watcher = new SystemThresholdWatcher(percentThreshold: 90, sustainedSamples: 2);
        watcher.Observe(Load(95, 95));

        var alerts = watcher.Observe(Load(95, 95));

        Assert.Equal(["system.cpu", "system.memory"], alerts.Select(alert => alert.Key));
    }
}
