// SHAppBarMessage wrapper. A registration that outlives the process leaves the desktop work area shrunk, so
// every path that opens one has to close it.
using System.Runtime.InteropServices;
using JKBar.Core.Layout;

namespace JKBar.App.Interop;

internal static class AppBarInterop
{
    private const uint AbmNew = 0x00000000;
    private const uint AbmRemove = 0x00000001;
    private const uint AbmQueryPos = 0x00000002;
    private const uint AbmSetPos = 0x00000003;

    private const uint AbeTop = 1;

    /// <summary>The shell posts appbar notifications back on whatever message the registration asked for.</summary>
    internal const int CallbackMessage = 0x0400 + 0x521;

    internal const int NotifyPositionChanged = 0x00000001;
    internal const int NotifyFullScreenApp = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        internal uint CbSize;
        internal IntPtr Hwnd;
        internal uint CallbackMessage;
        internal uint Edge;
        internal NativeRect Rect;
        internal int LParam;
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern UIntPtr SHAppBarMessage(uint message, ref AppBarData data);

    internal static bool Register(IntPtr hwnd)
    {
        var data = Describe(hwnd);
        data.CallbackMessage = CallbackMessage;

        return SHAppBarMessage(AbmNew, ref data) != UIntPtr.Zero;
    }

    internal static void Unregister(IntPtr hwnd)
    {
        var data = Describe(hwnd);
        SHAppBarMessage(AbmRemove, ref data);
    }

    /// <summary>
    /// Asks the shell where a band of <paramref name="height"/> may sit on the top edge and claims it. The shell
    /// answers with a rect it is willing to give, which is why the height is re-applied before claiming.
    /// </summary>
    internal static NotchGeometry.Rect Claim(IntPtr hwnd, NotchGeometry.Rect screen, int height)
    {
        var data = Describe(hwnd);
        data.Edge = AbeTop;
        data.Rect = new NativeRect
        {
            Left = screen.Left,
            Top = screen.Top,
            Right = screen.Right,
            Bottom = screen.Top + height
        };

        SHAppBarMessage(AbmQueryPos, ref data);
        data.Rect.Bottom = data.Rect.Top + height;
        SHAppBarMessage(AbmSetPos, ref data);

        return new NotchGeometry.Rect(data.Rect.Left, data.Rect.Top, data.Rect.Right, data.Rect.Bottom);
    }

    private static AppBarData Describe(IntPtr hwnd) => new()
    {
        CbSize = (uint)Marshal.SizeOf<AppBarData>(),
        Hwnd = hwnd
    };
}
