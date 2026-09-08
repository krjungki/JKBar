// Reads Windows GPU Engine utilization through PDH without creating a Direct3D device.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using JKBar.Core.Interop;

namespace JKBar.Core.Metrics;

[SupportedOSPlatform("windows")]
internal sealed class GpuUsageCounter : IDisposable
{
    private const uint PdhSuccess = 0;
    private const uint PdhMoreData = 0x800007D2;

    private IntPtr _query = IntPtr.Zero;
    private IntPtr _counter = IntPtr.Zero;
    private bool _primed;

    internal bool IsAvailable { get; private set; }

    internal GpuUsageCounter()
    {
        try
        {
            if (NativeMethods.PdhOpenQueryW(IntPtr.Zero, IntPtr.Zero, out _query) != PdhSuccess)
            {
                return;
            }

            IsAvailable = NativeMethods.PdhAddEnglishCounterW(
                _query,
                @"\GPU Engine(*)\Utilization Percentage",
                IntPtr.Zero,
                out _counter) == PdhSuccess;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            IsAvailable = false;
        }
    }

    internal double Read()
    {
        if (!IsAvailable || NativeMethods.PdhCollectQueryData(_query) != PdhSuccess)
        {
            return 0;
        }

        if (!_primed)
        {
            _primed = true;
            return 0;
        }

        var format = NativeMethods.PdhFmtDouble | NativeMethods.PdhFmtNoCap100;
        uint size = 0;
        uint count = 0;
        if (NativeMethods.PdhGetFormattedCounterArrayW(
                _counter, format, ref size, ref count, IntPtr.Zero) != PdhMoreData
            || size == 0
            || count == 0)
        {
            return 0;
        }

        var buffer = Marshal.AllocHGlobal(checked((int)size));
        try
        {
            if (NativeMethods.PdhGetFormattedCounterArrayW(
                    _counter, format, ref size, ref count, buffer) != PdhSuccess)
            {
                return 0;
            }

            var stride = Marshal.SizeOf<NativeMethods.PdhFmtCounterValueItem>();
            var samples = new List<KeyValuePair<string, double>>(checked((int)count));
            for (var index = 0; index < count; index++)
            {
                var address = IntPtr.Add(buffer, checked((int)index * stride));
                var item = Marshal.PtrToStructure<NativeMethods.PdhFmtCounterValueItem>(address);
                if (item.Value.CStatus > 1 || item.Name == IntPtr.Zero)
                {
                    continue;
                }

                var name = Marshal.PtrToStringUni(item.Name);
                if (!string.IsNullOrEmpty(name))
                {
                    samples.Add(new KeyValuePair<string, double>(name, item.Value.DoubleValue));
                }
            }

            return GpuUsageMath.OverallPercent(samples);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    internal void ResetBaseline() => _primed = false;

    public void Dispose()
    {
        if (_query != IntPtr.Zero)
        {
            NativeMethods.PdhCloseQuery(_query);
            _query = IntPtr.Zero;
        }

        IsAvailable = false;
    }
}