// Ported from JKMon.Core/Metrics/MetricsSnapshot.cs.
namespace JKBar.Core.Metrics;

/// <summary>Display-ready metric values derived from two consecutive samples.</summary>
public readonly record struct MetricsSnapshot(
    double CpuPercent,
    double MemoryPercent,
    double NetworkInBytesPerSecond,
    double NetworkOutBytesPerSecond,
    double DiskReadBytesPerSecond,
    double DiskWriteBytesPerSecond,
    double GpuPercent = 0,
    bool GpuAvailable = false)
{
    public static MetricsSnapshot Empty => new(0, 0, 0, 0, 0, 0);
}
