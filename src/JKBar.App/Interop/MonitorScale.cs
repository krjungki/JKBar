// Reads the scale of the monitor a rectangle sits on.
// Ported from JKMon (packages/JKMon/src/JKMon.App/Interop/MonitorCatalog.cs). Keep behaviour changes in sync.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace JKBar.App.Interop;

[SupportedOSPlatform("windows")]
internal static class MonitorScale
{
    private const uint MonitorDefaultToNearest = 2;
    private const int EffectiveDpi = 0;

    /// <summary>
    /// The window's own DeviceDpi keeps reporting a display that has been unplugged, so the scale is read from
    /// the monitor under the given point instead.
    /// </summary>
    internal static double At(int x, int y) => DpiAt(x, y) / 96d;

    private static uint DpiAt(int x, int y)
    {
        try
        {
            var monitor = MonitorFromPoint(new Point { X = x, Y = y }, MonitorDefaultToNearest);
            if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, EffectiveDpi, out var dpiX, out _) == 0)
            {
                return dpiX;
            }
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
            // Older shells without shcore simply run unscaled.
        }

        return 96;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        internal int X;
        internal int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point point, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
}
