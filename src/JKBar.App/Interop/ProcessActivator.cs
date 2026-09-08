// Raises a watched application's window when its icon in the band is clicked.
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;

namespace JKBar.App.Interop;

[SupportedOSPlatform("windows")]
internal static class ProcessActivator
{
    private const int SwRestore = 9;
    private const uint GwOwner = 4;
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const long WsSysMenu = 0x00080000;
    private const long WsExToolWindow = 0x00000080;

    internal static void Activate(WatchedProcess watched)
    {
        var (window, owners) = ShownWindowOf(watched.MatchKey);
        switch (ProcessIconHitTest.Decide(window != IntPtr.Zero, IsRunnableFile(watched.Path) || owners.Count > 0))
        {
            case ProcessActivation.Focus:
                Focus(window);
                break;
            case ProcessActivation.Launch:
                Open(watched, owners);
                break;
        }
    }

    /// <summary>
    /// Only windows that are on screen count, minimised ones included. A window an application hid on its way to
    /// the tray is deliberately left alone: forcing it visible from outside puts up a frame the application never
    /// draws into, which is the empty window this used to produce.
    /// </summary>
    private static (IntPtr Window, HashSet<uint> Owners) ShownWindowOf(string processName)
    {
        Process[] running;
        try
        {
            running = Process.GetProcessesByName(processName);
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception)
        {
            return (IntPtr.Zero, []);
        }

        var main = IntPtr.Zero;
        var owners = new HashSet<uint>();
        foreach (var process in running)
        {
            try
            {
                owners.Add((uint)process.Id);
                if (main == IntPtr.Zero && process.MainWindowHandle != IntPtr.Zero
                    && IsWindowVisible(process.MainWindowHandle))
                {
                    main = process.MainWindowHandle;
                }
            }
            catch (Exception error) when (error is InvalidOperationException or Win32Exception)
            {
                // The process ended while it was being inspected, which just means there is nothing to raise.
            }
            finally
            {
                process.Dispose();
            }
        }

        return (main != IntPtr.Zero ? main : ShownAppWindow(owners), owners);
    }

    private static IntPtr ShownAppWindow(HashSet<uint> owners)
    {
        if (owners.Count == 0)
        {
            return IntPtr.Zero;
        }

        var found = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out var owner);
            if (!owners.Contains(owner))
            {
                return true;
            }

            var score = ProcessWindowChoice.Score(
                titled: GetWindowTextLength(window) > 0,
                owned: GetWindow(window, GwOwner) != IntPtr.Zero,
                toolWindow: (GetWindowLongPtr(window, GwlExStyle).ToInt64() & WsExToolWindow) != 0,
                appFrame: (GetWindowLongPtr(window, GwlStyle).ToInt64() & WsSysMenu) != 0,
                visible: IsWindowVisible(window));

            if (score != ProcessWindowChoice.VisibleAppWindow)
            {
                return true;
            }

            found = window;
            return false;
        }, IntPtr.Zero);

        return found;
    }

    private static void Focus(IntPtr window)
    {
        if (IsIconic(window))
        {
            ShowWindow(window, SwRestore);
        }

        if (SetForegroundWindow(window))
        {
            return;
        }

        // Windows only lets the thread that already owns the foreground hand it over, so its input queue is
        // borrowed for the moment the switch takes. The band itself never takes focus, being WS_EX_NOACTIVATE.
        var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var targetThread = GetWindowThreadProcessId(window, out _);
        if (foregroundThread == 0 || targetThread == 0 || foregroundThread == targetThread
            || !AttachThreadInput(foregroundThread, targetThread, true))
        {
            return;
        }

        try
        {
            SetForegroundWindow(window);
            BringWindowToTop(window);
        }
        finally
        {
            AttachThreadInput(foregroundThread, targetThread, false);
        }
    }

    /// <summary>
    /// Starting the application again is how it is asked to show itself: the copy already running takes over and
    /// opens its own window, drawn properly, rather than the empty frame an outside caller can put up.
    /// </summary>
    private static void Open(WatchedProcess watched, HashSet<uint> owners)
    {
        // A packaged executable cannot be run from its own folder, so the shell is asked to activate it instead.
        foreach (var owner in owners)
        {
            if (PackagedApp.UserModelIdOf((int)owner) is { } identifier && PackagedApp.Open(identifier))
            {
                return;
            }
        }

        if (!IsRunnableFile(watched.Path))
        {
            return;
        }

        try
        {
            // Shell execution is deliberately off: the entry is only ever meant to start this one executable.
            using var started = Process.Start(new ProcessStartInfo(watched.Path)
            {
                UseShellExecute = false,
                WorkingDirectory = System.IO.Path.GetDirectoryName(watched.Path) ?? string.Empty
            });
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or IOException
            or ObjectDisposedException)
        {
            // Nothing to tell the user that they cannot already see: the window simply did not come up.
        }
    }

    /// <summary>Only the executable the user pointed at, and only if it is still there.</summary>
    private static bool IsRunnableFile(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
        && File.Exists(path);

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint attachTo, uint attachFrom, [MarshalAs(UnmanagedType.Bool)] bool attach);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextLengthW")]
    private static extern int GetWindowTextLength(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint command);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
}
