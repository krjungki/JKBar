// Ported from JKMon (packages/JKMon/src/JKMon.Core/Sync). Keep behaviour changes in sync with the original.
using System.Diagnostics;
using System.Runtime.Versioning;
using JKBar.Core.Interop;

namespace JKBar.Core.Sync;

/// <summary>
/// Sums the OneDrive processes' I/O transfer counters. OneDrive does not expose sync state to third-party
/// processes, so transfer activity is the only available signal that a sync is in progress.
/// </summary>
[SupportedOSPlatform("windows")]
public class OneDriveActivityProbe
{
    /// <summary>Sync root registrations survive the app being closed, so the process is what proves it is active.</summary>
    public virtual bool IsRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName("OneDrive");
            foreach (var process in processes)
            {
                process.Dispose();
            }

            return processes.Length > 0;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public virtual long TotalTransferBytes()
    {
        ulong total = 0;
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName("OneDrive");
        }
        catch (InvalidOperationException)
        {
            return 0;
        }

        foreach (var process in processes)
        {
            try
            {
                if (NativeMethods.GetProcessIoCounters(process.Handle, out var counters))
                {
                    total += counters.ReadTransferCount + counters.WriteTransferCount + counters.OtherTransferCount;
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // A process that exits between enumeration and query simply contributes nothing.
            }
            finally
            {
                process.Dispose();
            }
        }

        return total > long.MaxValue ? long.MaxValue : (long)total;
    }
}
