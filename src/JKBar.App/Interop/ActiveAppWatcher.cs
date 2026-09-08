// Names the application the user is working in, for the left side of the band.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace JKBar.App.Interop;

[SupportedOSPlatform("windows")]
internal sealed class ActiveAppWatcher
{
    private const string PackagedWindowHost = "ApplicationFrameHost";

    private IntPtr _window;

    internal string? Current { get; private set; }

    /// <summary>Returns true when the name changed. The name is only re-read when the foreground window does.</summary>
    internal bool Refresh()
    {
        var window = GetForegroundWindow();
        if (window == _window)
        {
            return false;
        }

        _window = window;
        var name = window == IntPtr.Zero ? null : NameOf(window);
        if (name == Current)
        {
            return false;
        }

        Current = name;
        return true;
    }

    private static string? NameOf(IntPtr window)
    {
        GetWindowThreadProcessId(window, out var pid);
        if (pid == 0)
        {
            return null;
        }

        var name = DescribeProcess(pid);
        return name == PackagedWindowHost ? DescribeProcess(PackagedContentOwner(window, pid) ?? pid) : name;
    }

    private static string? DescribeProcess(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            var description = process.MainModule?.FileVersionInfo.FileDescription?.Trim();
            return string.IsNullOrEmpty(description) ? process.ProcessName : description;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException
            or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>A packaged app's window is hosted by another process; the real app owns a child window inside it.</summary>
    private static uint? PackagedContentOwner(IntPtr window, uint hostPid)
    {
        uint owner = 0;
        EnumChildWindows(window, (child, _) =>
        {
            GetWindowThreadProcessId(child, out var childPid);
            if (childPid == 0 || childPid == hostPid)
            {
                return true;
            }

            owner = childPid;
            return false;
        }, IntPtr.Zero);

        return owner == 0 ? null : owner;
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr parameter);
}
