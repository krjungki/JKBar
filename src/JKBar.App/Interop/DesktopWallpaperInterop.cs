// Reads what Windows paints on the desktop behind the band: the image, how it is fitted and the fill colour.
using System.Runtime.InteropServices;
using JKBar.Core.Layout;
using Microsoft.Win32;

namespace JKBar.App.Interop;

/// <param name="ImagePath">Null for a solid-colour desktop.</param>
internal sealed record WallpaperSource(
    string? ImagePath,
    WallpaperFit Fit,
    Color Background,
    Rectangle Monitor,
    Rectangle VirtualDesktop);

internal static class DesktopWallpaperInterop
{
    private static readonly Guid DesktopWallpaperClass = new("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD");

    /// <summary>The copy Windows re-encodes for display. Used when the configured file has gone away.</summary>
    internal static string TranscodedPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft", "Windows", "Themes", "TranscodedWallpaper");

    /// <summary>Returns null when the shell will not say, so the band keeps the user's own appearance.</summary>
    internal static WallpaperSource? Read(Rectangle band)
    {
        IDesktopWallpaper? wallpaper = null;
        try
        {
            var type = Type.GetTypeFromCLSID(DesktopWallpaperClass, throwOnError: false);
            if (type is null || Activator.CreateInstance(type) is not IDesktopWallpaper created)
            {
                return null;
            }

            wallpaper = created;
            if (FindMonitor(wallpaper, band) is not { } found)
            {
                return null;
            }

            var (monitorId, monitor) = found;

            var path = wallpaper.GetWallpaper(monitorId, out var file) == 0 && !string.IsNullOrWhiteSpace(file)
                ? file
                : null;
            var fit = wallpaper.GetPosition(out var position) == 0 && Enum.IsDefined((WallpaperFit)position)
                ? (WallpaperFit)position
                : WallpaperFit.Fill;
            var background = wallpaper.GetBackgroundColor(out var colourRef) == 0
                ? Color.FromArgb((int)(colourRef & 0xFF), (int)((colourRef >> 8) & 0xFF), (int)((colourRef >> 16) & 0xFF))
                : Color.Black;

            return new WallpaperSource(path, fit, background, monitor, SystemInformation.VirtualScreen);
        }
        catch (Exception error) when (error is COMException or InvalidCastException or UnauthorizedAccessException)
        {
            return null;
        }
        finally
        {
            if (wallpaper is not null)
            {
                Marshal.ReleaseComObject(wallpaper);
            }
        }
    }

    /// <summary>Settings > Personalisation > Colours > Transparency effects.</summary>
    internal static bool TransparencyEffectsEnabled() =>
        Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "EnableTransparency",
            1) is not int enabled || enabled != 0;

    /// <summary>The band lies along the top of exactly one monitor, so the one it overlaps most is the one behind it.</summary>
    private static (string Id, Rectangle Bounds)? FindMonitor(IDesktopWallpaper wallpaper, Rectangle band)
    {
        if (wallpaper.GetMonitorDevicePathCount(out var count) != 0)
        {
            return null;
        }

        (string Id, Rectangle Bounds)? best = null;
        var bestArea = 0L;
        for (var index = 0u; index < count; index++)
        {
            if (wallpaper.GetMonitorDevicePathAt(index, out var id) != 0 || string.IsNullOrEmpty(id)
                || wallpaper.GetMonitorRECT(id, out var rect) != 0)
            {
                continue;
            }

            var bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            var overlap = Rectangle.Intersect(bounds, band);
            var area = (long)overlap.Width * overlap.Height;
            if (area > bestArea)
            {
                bestArea = area;
                best = (id, bounds);
            }
        }

        return best;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    // Declared in vtable order up to the last method used; the slideshow methods that follow are not needed.
    [ComImport]
    [Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWallpaper
    {
        [PreserveSig]
        int SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

        [PreserveSig]
        int GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId, [MarshalAs(UnmanagedType.LPWStr)] out string? wallpaper);

        [PreserveSig]
        int GetMonitorDevicePathAt(uint monitorIndex, [MarshalAs(UnmanagedType.LPWStr)] out string? monitorId);

        [PreserveSig]
        int GetMonitorDevicePathCount(out uint count);

        [PreserveSig]
        int GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorId, out NativeRect displayRect);

        [PreserveSig]
        int SetBackgroundColor(uint color);

        [PreserveSig]
        int GetBackgroundColor(out uint color);

        [PreserveSig]
        int SetPosition(int position);

        [PreserveSig]
        int GetPosition(out int position);
    }
}
