// Ported from JKMon's TrayIconFactory: the mark is drawn in code so no binary asset has to ship with the source.
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace JKBar.App;

internal static class TrayIconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    internal static (Icon Icon, IntPtr Handle) Create()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var brush = new SolidBrush(Color.FromArgb(235, 245, 245, 247));
            using var path = new GraphicsPath();
            path.AddLine(6, 6, 26, 6);
            path.AddLine(26, 6, 26, 16);
            path.AddArc(18, 12, 8, 8, 0, 90);
            path.AddArc(6, 12, 8, 8, 90, 90);
            path.CloseFigure();
            g.FillPath(brush, path);
        }

        var handle = bitmap.GetHicon();
        return (Icon.FromHandle(handle), handle);
    }

    internal static void Destroy(IntPtr handle)
    {
        if (handle != IntPtr.Zero)
        {
            DestroyIcon(handle);
        }
    }
}
