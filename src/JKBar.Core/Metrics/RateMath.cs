// Ported from JKMon.Core/Metrics/RateMath.cs.
namespace JKBar.Core.Metrics;

/// <summary>Pure conversions from raw counter pairs to rates, isolated from the OS so they can be tested.</summary>
public static class RateMath
{
    public static double CpuPercent(in MetricSample previous, in MetricSample current)
    {
        // GetSystemTimes reports kernel time inclusive of idle time.
        var idle = Delta(previous.CpuIdleTicks, current.CpuIdleTicks);
        var kernel = Delta(previous.CpuKernelTicks, current.CpuKernelTicks);
        var user = Delta(previous.CpuUserTicks, current.CpuUserTicks);
        var total = kernel + user;
        if (total == 0)
        {
            return 0;
        }

        return Clamp01((total - idle) / (double)total) * 100d;
    }

    public static double MemoryPercent(in MetricSample current)
    {
        if (current.MemoryTotalBytes == 0)
        {
            return 0;
        }

        var used = current.MemoryTotalBytes >= current.MemoryAvailableBytes
            ? current.MemoryTotalBytes - current.MemoryAvailableBytes
            : 0UL;

        return Clamp01(used / (double)current.MemoryTotalBytes) * 100d;
    }

    public static double BytesPerSecond(ulong previousBytes, ulong currentBytes, TimeSpan elapsed) =>
        elapsed <= TimeSpan.Zero ? 0 : Delta(previousBytes, currentBytes) / elapsed.TotalSeconds;

    public static MetricsSnapshot Compose(in MetricSample previous, in MetricSample current)
    {
        var elapsed = current.Timestamp - previous.Timestamp;

        return new MetricsSnapshot(
            CpuPercent(previous, current),
            MemoryPercent(current),
            BytesPerSecond(previous.NetworkBytesReceived, current.NetworkBytesReceived, elapsed),
            BytesPerSecond(previous.NetworkBytesSent, current.NetworkBytesSent, elapsed),
            Math.Max(0, current.DiskReadBytesPerSecond),
            Math.Max(0, current.DiskWriteBytesPerSecond));
    }

    /// <summary>Counters reset when an adapter disappears or the machine resumes, so treat regressions as no progress.</summary>
    private static ulong Delta(ulong previous, ulong current) => current >= previous ? current - previous : 0UL;

    private static double Clamp01(double value) => Math.Clamp(value, 0d, 1d);
}
