// Ported alongside JKMon's RateMath. The counter quirks these cover are the reason that code is worth reusing.
using JKBar.Core.Metrics;

namespace JKBar.Core.Tests;

public class RateMathTests
{
    private static MetricSample At(double seconds, ulong idle, ulong kernel, ulong user, ulong received = 0) =>
        new(DateTimeOffset.UnixEpoch.AddSeconds(seconds), idle, kernel, user, 0, 0, received, 0, 0, 0);

    [Fact]
    public void ReportsHalfBusyWhenHalfTheTicksAreIdle()
    {
        var percent = RateMath.CpuPercent(At(0, 0, 0, 0), At(1, 100, 150, 50));

        Assert.Equal(50d, percent, 3);
    }

    /// <summary>GetSystemTimes counts idle inside kernel time, so idle can equal the whole interval.</summary>
    [Fact]
    public void ReportsIdleAsZero()
    {
        Assert.Equal(0d, RateMath.CpuPercent(At(0, 0, 0, 0), At(1, 200, 200, 0)), 3);
    }

    [Fact]
    public void ReportsNoProgressWhenCountersGoBackwards()
    {
        var sample = At(1, 0, 0, 0, received: 500);
        var afterReset = At(2, 0, 0, 0, received: 10);

        Assert.Equal(0d, RateMath.BytesPerSecond(sample.NetworkBytesReceived, afterReset.NetworkBytesReceived, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void DividesBytesByTheElapsedInterval()
    {
        Assert.Equal(500d, RateMath.BytesPerSecond(0, 1000, TimeSpan.FromSeconds(2)));
    }

    /// <summary>Two samples with the same timestamp would otherwise divide by zero and report infinity.</summary>
    [Fact]
    public void ReportsZeroForAnEmptyInterval()
    {
        Assert.Equal(0d, RateMath.BytesPerSecond(0, 1000, TimeSpan.Zero));
    }

    [Fact]
    public void ReportsMemoryAsTheUsedShare()
    {
        var sample = new MetricSample(DateTimeOffset.UnixEpoch, 0, 0, 0, 1000, 250, 0, 0, 0, 0);

        Assert.Equal(75d, RateMath.MemoryPercent(sample), 3);
    }

    [Fact]
    public void ReportsMemoryAsZeroBeforeTheFirstReadSucceeds()
    {
        Assert.Equal(0d, RateMath.MemoryPercent(new MetricSample(DateTimeOffset.UnixEpoch, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
    }
}
