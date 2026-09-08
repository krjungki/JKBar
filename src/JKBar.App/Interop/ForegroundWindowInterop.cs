// Ported from JKMon.App/Interop/ForegroundWindowInterop.cs.
using System.Runtime.InteropServices;
using System.Text;
using JKBar.Core.Layout;
using JKBar.Core.Presentation;

namespace JKBar.App.Interop;

/// <summary>Reads what currently owns the screen, so the bar can get out of the way of a full-screen app.</summary>
internal static class ForegroundWindowInterop
{
    /// <summary>Classes that cover the monitor by nature and must never count as a full-screen application.</summary>
    private static readonly string[] ShellClasses =
        ["Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "SysListView32", "WindowsDashboard"];

    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        internal int Size;
        internal NativeRect Monitor;
        internal NativeRect Work;
        internal uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")]
    private static extern int GetClassName(IntPtr window, StringBuilder buffer, int maxCount);

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);

    internal static UserNotificationState NotificationState()
    {
        try
        {
            return SHQueryUserNotificationState(out var state) == 0
                ? (UserNotificationState)state
                : UserNotificationState.Unknown;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return UserNotificationState.Unknown;
        }
    }

    /// <summary>Which display a window sits on. A hidden window keeps its position, so this works while suppressed.</summary>
    internal static IntPtr MonitorOf(IntPtr window) =>
        window == IntPtr.Zero ? IntPtr.Zero : MonitorFromWindow(window, MonitorDefaultToNearest);

    /// <summary>Reports the shell when there is no usable foreground window, which reads as "not full screen".</summary>
    internal static (bool IsShell, IntPtr Monitor, NotchGeometry.Rect Window, NotchGeometry.Rect Bounds) Foreground()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero || !GetWindowRect(window, out var rect))
        {
            return (true, IntPtr.Zero, default, default);
        }

        var monitor = MonitorFromWindow(window, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            return (true, IntPtr.Zero, default, default);
        }

        return (
            IsShell(window),
            monitor,
            new NotchGeometry.Rect(rect.Left, rect.Top, rect.Right, rect.Bottom),
            new NotchGeometry.Rect(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right, info.Monitor.Bottom));
    }

    private static bool IsShell(IntPtr window)
    {
        var buffer = new StringBuilder(256);
        var length = GetClassName(window, buffer, buffer.Capacity);

        return length <= 0 || ShellClasses.Contains(buffer.ToString(), StringComparer.Ordinal);
    }
}
