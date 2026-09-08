// Ported from JKMon.App/Interop/OverlayWindowInterop.cs, trimmed to what a click-through layered bar needs.
using System.Runtime.InteropServices;
using JKBar.Core.Layout;

namespace JKBar.App.Interop;

/// <summary>
/// Window style, placement and per-pixel-alpha painting through documented Win32 calls only. Deliberately no WPF:
/// a WPF window creates a D3D9 device and holds the display driver's user-mode DLLs, which blocks GPU switching
/// on hybrid-graphics laptops. JKMon proved this and the same rule applies here.
/// </summary>
internal static class NotchWindowInterop
{
    internal const int WsExToolWindow = 0x00000080;
    internal const int WsExTransparent = 0x00000020;
    internal const int WsExNoActivate = 0x08000000;
    internal const int WsExLayered = 0x00080000;

    private static readonly IntPtr HwndTopMost = new(-1);
    private static readonly IntPtr HwndNoTopMost = new(-2);
    private static readonly IntPtr HwndBottom = new(1);

    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    internal const int WmDpiChanged = 0x02E0;
    internal const int WmDisplayChange = 0x007E;
    internal const int WmSettingChange = 0x001A;
    internal const int WmWindowPosChanging = 0x0046;
    internal const int WmDeviceChange = 0x0219;

    internal const int DeviceArrived = 0x8000;
    internal const int DeviceRemoved = 0x8004;
    internal const int DeviceTypeVolume = 2;

    /// <summary>The prefix of DEV_BROADCAST_VOLUME; volume broadcasts reach every top-level window unregistered.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct VolumeBroadcast
    {
        internal int Size;
        internal int DeviceType;
        internal int Reserved;
        internal uint UnitMask;
        internal ushort Flags;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPos
    {
        internal IntPtr Hwnd;
        internal IntPtr HwndInsertAfter;
        internal int X;
        internal int Y;
        internal int Cx;
        internal int Cy;
        internal uint Flags;
    }

    /// <summary>
    /// Holds the window in the topmost band by editing the pending WINDOWPOS. Calling SetWindowPos from inside
    /// WM_WINDOWPOSCHANGING would cancel the very move that raised the message.
    /// </summary>
    internal static void PinToTop(IntPtr lParam) => Pin(lParam, HwndTopMost);

    /// <summary>The same treatment for the desktop mode, where the shell keeps trying to lift the window.</summary>
    internal static void PinToBottom(IntPtr lParam) => Pin(lParam, HwndBottom);

    /// <summary>Keeps one window immediately behind another, so a reorder can never put them the wrong way round.</summary>
    internal static void PinBehind(IntPtr lParam, IntPtr other) => Pin(lParam, other);

    private static void Pin(IntPtr lParam, IntPtr insertAfter)
    {
        if (lParam == IntPtr.Zero)
        {
            return;
        }

        var position = Marshal.PtrToStructure<WindowPos>(lParam);
        position.HwndInsertAfter = insertAfter;
        position.Flags &= ~SwpNoZOrder;
        Marshal.StructureToPtr(position, lParam, fDeleteOld: false);
    }

    internal static void RaiseToTop(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero)
        {
            SetWindowPos(hwnd, HwndTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        }
    }

    /// <summary>Desktop pinning keeps the window bottom-most rather than reparenting it under the wallpaper.</summary>
    internal static void SendToBottom(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        SetWindowPos(hwnd, HwndNoTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        SetWindowPos(hwnd, HwndBottom, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

    internal static NotchGeometry.Rect GetBounds(IntPtr hwnd) =>
        hwnd != IntPtr.Zero && GetWindowRect(hwnd, out var rect)
            ? new NotchGeometry.Rect(rect.Left, rect.Top, rect.Right, rect.Bottom)
            : default;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        internal int Cx;
        internal int Cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlendFunction
    {
        internal byte BlendOp;
        internal byte BlendFlags;
        internal byte SourceConstantAlpha;
        internal byte AlphaFormat;
    }

    private const byte AcSrcOver = 0x00;
    private const byte AcSrcAlpha = 0x01;
    private const int UlwAlpha = 0x00000002;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindow(
        IntPtr hwnd, IntPtr hdcDst, ref NativePoint pptDst, ref NativeSize psize,
        IntPtr hdcSrc, ref NativePoint pptSrc, int crKey, ref BlendFunction pblend, int dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr handle);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr handle);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);

    /// <summary>
    /// Moves, resizes and repaints in one call. The window manager composites a layered window from the bitmap
    /// handed over here, which is what gives per-pixel alpha with no WM_PAINT and no flicker.
    /// </summary>
    internal static void PushLayeredSurface(IntPtr hwnd, Bitmap bitmap, int x, int y)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var screen = GetDC(IntPtr.Zero);
        var memory = CreateCompatibleDC(screen);
        var surface = bitmap.GetHbitmap(Color.FromArgb(0));
        var previous = SelectObject(memory, surface);

        try
        {
            var position = new NativePoint { X = x, Y = y };
            var size = new NativeSize { Cx = bitmap.Width, Cy = bitmap.Height };
            var origin = new NativePoint { X = 0, Y = 0 };
            var blend = new BlendFunction
            {
                BlendOp = AcSrcOver,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = AcSrcAlpha
            };

            UpdateLayeredWindow(hwnd, screen, ref position, ref size, memory, ref origin, 0, ref blend, UlwAlpha);
        }
        finally
        {
            SelectObject(memory, previous);
            DeleteObject(surface);
            DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }
}
