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
    private readonly int _sessionId;

    public OneDriveActivityProbe() : this(CurrentSessionId())
    {
    }

    internal OneDriveActivityProbe(int sessionId)
    {
        _sessionId = sessionId;
    }

    private static int CurrentSessionId()
    {
        using var process = Process.GetCurrentProcess();
        return process.SessionId;
    }

    internal bool IsCurrentSession(Process process)
    {
        try
        {
            return process.SessionId == _sessionId;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>Sync root registrations survive the app being closed, so the process is what proves it is active.</summary>
    public virtual bool IsRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName("OneDrive");
            var running = false;
            foreach (var process in processes)
            {
                running |= IsCurrentSession(process);
                process.Dispose();
            }

            return running;
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
                if (IsCurrentSession(process) && NativeMethods.GetProcessIoCounters(process.Handle, out var counters))
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
