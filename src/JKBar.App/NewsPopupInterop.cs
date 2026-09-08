// Window shaping for the news panel: a soft shadow and rounded corners without owner drawing the frame.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace JKBar.App;

[SupportedOSPlatform("windows")]
internal static class NewsPopupInterop
{
    internal const int CsDropShadow = 0x00020000;

    internal static void RoundCorners(IntPtr window, int width, int height, int radius)
    {
        var region = CreateRoundRectRgn(0, 0, width + 1, height + 1, radius, radius);
        if (region == IntPtr.Zero)
        {
            return;
        }

        // The window owns the region once this returns, so it must not be deleted here.
        if (SetWindowRgn(window, region, true) == 0)
        {
            DeleteObject(region);
        }
    }

    /// <summary>
    /// Hands the press to the window manager as if it had landed on a title bar, which is what gives the drag its
    /// snapping and edge behaviour. Doing the move by hand in mouse events would lose both.
    /// </summary>
    internal static void DragByCaption(IntPtr window)
    {
        const int wmNcLButtonDown = 0x00A1;
        const int htCaption = 2;

        ReleaseCapture();
        SendMessage(window, wmNcLButtonDown, htCaption, IntPtr.Zero);
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, int wParam, IntPtr lParam);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr window, IntPtr region, [MarshalAs(UnmanagedType.Bool)] bool redraw);
}
