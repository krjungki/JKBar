// Ported from JKMon.Core/Metrics/MetricSample.cs. The per-core fields are dropped: a one-line band has no room for them.
namespace JKBar.Core.Metrics;

/// <summary>Raw monotonic counters read from the OS at a point in time.</summary>
public readonly record struct MetricSample(
    DateTimeOffset Timestamp,
    ulong CpuIdleTicks,
    ulong CpuKernelTicks,
    ulong CpuUserTicks,
    ulong MemoryTotalBytes,
    ulong MemoryAvailableBytes,
    ulong NetworkBytesReceived,
    ulong NetworkBytesSent,
    double DiskReadBytesPerSecond,
    double DiskWriteBytesPerSecond);
