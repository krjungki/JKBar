// Ported from JKMon.Core/Metrics/SystemMetricsCollector.cs, without the per-core counter.
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using JKBar.Core.Interop;

namespace JKBar.Core.Metrics;

/// <summary>Reads raw Windows counters. Failures degrade to zeroed fields rather than throwing at the UI.</summary>
[SupportedOSPlatform("windows")]
public sealed class SystemMetricsCollector : IDisposable
{
    private readonly DiskThroughputCounter _disk = new();
    private readonly GpuUsageCounter _gpu = new();
    private MetricSample? _previous;

    public MetricsSnapshot Read()
    {
        var current = Sample();
        var previous = _previous;
        _previous = current;
        var gpu = _gpu.Read();

        // The first sample only establishes a baseline for the delta-based rates.
        return (previous is null ? MetricsSnapshot.Empty : RateMath.Compose(previous.Value, current)) with
        {
            GpuPercent = gpu,
            GpuAvailable = _gpu.IsAvailable
        };
    }

    /// <summary>
    /// Forgets the last sample so the next read starts a fresh interval. Without this, resuming after a pause would
    /// divide a whole pause worth of counters by one interval and report a rate that never happened.
    /// </summary>
    public void ResetBaseline()
    {
        _previous = null;
        _disk.ResetBaseline();
        _gpu.ResetBaseline();
    }

    private MetricSample Sample()
    {
        ulong idle = 0, kernel = 0, user = 0;
        if (NativeMethods.GetSystemTimes(out var idleRaw, out var kernelRaw, out var userRaw))
        {
            idle = unchecked((ulong)idleRaw);
            kernel = unchecked((ulong)kernelRaw);
            user = unchecked((ulong)userRaw);
        }

        ulong totalPhys = 0, availPhys = 0;
        var memory = new NativeMethods.MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<NativeMethods.MemoryStatusEx>()
        };

        if (NativeMethods.GlobalMemoryStatusEx(ref memory))
        {
            totalPhys = memory.TotalPhys;
            availPhys = memory.AvailPhys;
        }

        var (received, sent) = ReadNetworkTotals();
        var (read, write) = _disk.Read();

        return new MetricSample(
            DateTimeOffset.UtcNow, idle, kernel, user, totalPhys, availPhys, received, sent, read, write);
    }

    private static (ulong Received, ulong Sent) ReadNetworkTotals()
    {
        ulong received = 0;
        ulong sent = 0;

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback
                    || nic.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                var stats = nic.GetIPStatistics();
                received += (ulong)Math.Max(0, stats.BytesReceived);
                sent += (ulong)Math.Max(0, stats.BytesSent);
            }
        }
        catch (NetworkInformationException)
        {
            return (0, 0);
        }

        return (received, sent);
    }

    public void Dispose()
    {
        _disk.Dispose();
        _gpu.Dispose();
    }
}
