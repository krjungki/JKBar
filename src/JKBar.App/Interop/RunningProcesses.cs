// Lists what is running right now; the decision about which ones matter lives in JKBar.Core.
using System.Diagnostics;
using System.Runtime.Versioning;

namespace JKBar.App.Interop;

[SupportedOSPlatform("windows")]
internal static class RunningProcesses
{
    /// <summary>
    /// How many processes carry each name. Names only: reading each one's file path would need rights JKBar
    /// deliberately does not ask for.
    /// </summary>
    internal static IReadOnlyDictionary<string, int> Counts()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Process[] running;
        try
        {
            running = Process.GetProcesses();
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return counts;
        }

        foreach (var process in running)
        {
            try
            {
                counts[process.ProcessName] = counts.GetValueOrDefault(process.ProcessName) + 1;
            }
            catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // The process ended between listing and reading it, which only means it is not running.
            }
            finally
            {
                process.Dispose();
            }
        }

        return counts;
    }
}
