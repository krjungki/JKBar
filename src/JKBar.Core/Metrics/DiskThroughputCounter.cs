// Ported from JKMon.Core/Metrics/DiskThroughputCounter.cs.
using System.Runtime.Versioning;
using JKBar.Core.Interop;

namespace JKBar.Core.Metrics;

/// <summary>PDH physical-disk throughput using English counter paths so a localized Windows still resolves them.</summary>
[SupportedOSPlatform("windows")]
internal sealed class DiskThroughputCounter : IDisposable
{
    private const uint PdhSuccess = 0;

    private IntPtr _query = IntPtr.Zero;
    private IntPtr _readCounter = IntPtr.Zero;
    private IntPtr _writeCounter = IntPtr.Zero;
    private bool _available;
    private bool _primed;

    internal DiskThroughputCounter()
    {
        try
        {
            if (NativeMethods.PdhOpenQueryW(IntPtr.Zero, IntPtr.Zero, out _query) != PdhSuccess)
            {
                return;
            }

            var readOk = NativeMethods.PdhAddEnglishCounterW(
                _query, @"\PhysicalDisk(_Total)\Disk Read Bytes/sec", IntPtr.Zero, out _readCounter) == PdhSuccess;
            var writeOk = NativeMethods.PdhAddEnglishCounterW(
                _query, @"\PhysicalDisk(_Total)\Disk Write Bytes/sec", IntPtr.Zero, out _writeCounter) == PdhSuccess;

            _available = readOk && writeOk;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            _available = false;
        }
    }

    internal (double Read, double Write) Read()
    {
        if (!_available || NativeMethods.PdhCollectQueryData(_query) != PdhSuccess)
        {
            return (0, 0);
        }

        // Rate counters need two collections before the first formatted value is meaningful.
        if (!_primed)
        {
            _primed = true;
            return (0, 0);
        }

        return (ReadCounter(_readCounter), ReadCounter(_writeCounter));
    }

    /// <summary>PDH averages over the gap between collections, so a pause has to be followed by a fresh prime.</summary>
    internal void ResetBaseline() => _primed = false;

    private static double ReadCounter(IntPtr counter)
    {
        var format = NativeMethods.PdhFmtDouble | NativeMethods.PdhFmtNoCap100;
        if (NativeMethods.PdhGetFormattedCounterValue(counter, format, IntPtr.Zero, out var value) != PdhSuccess)
        {
            return 0;
        }

        return double.IsFinite(value.DoubleValue) && value.DoubleValue > 0 ? value.DoubleValue : 0;
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero)
        {
            NativeMethods.PdhCloseQuery(_query);
            _query = IntPtr.Zero;
        }

        _available = false;
    }
}
