// Ported from JKMon (packages/JKMon/src/JKMon.App/ProviderIconResolver.cs). Keep behaviour changes in sync.
// Icons come from the installed applications, so no third-party artwork ships with this repository.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using JKBar.Core.Sync;
using Microsoft.Win32;

using Path = System.IO.Path;

namespace JKBar.App.Sync;

[SupportedOSPlatform("windows")]
internal static class ProviderIconResolver
{
    private const string SyncRootManagerKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\SyncRootManager";

    private static readonly Dictionary<string, Bitmap> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The returned bitmap is cached and reused, so callers must not dispose it.</summary>
    internal static Bitmap? Resolve(string providerId, int pixelSize)
    {
        var key = $"{providerId}:{pixelSize}";
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        // A miss is not cached: the provider may simply not be running yet.
        var loaded = Load(providerId, pixelSize);
        if (loaded is not null)
        {
            Cache[key] = loaded;
        }

        return loaded;
    }

    /// <summary>The icon of a running process, for anything named by executable rather than by provider id.</summary>
    internal static Bitmap? ResolveProcess(string processName, int pixelSize)
    {
        var key = $"process:{processName}:{pixelSize}";
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var location = LocateRunningProcess(processName);
        var loaded = location is null ? null : Extract(location.Value.Path, location.Value.Index, pixelSize);
        if (loaded is not null)
        {
            Cache[key] = loaded;
        }

        return loaded;
    }

    /// <summary>The icon of an executable the user pointed at, so the file is read rather than the live process.</summary>
    internal static Bitmap? ResolveFile(string executablePath, int pixelSize)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        var key = $"file:{executablePath}:{pixelSize}";
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        Bitmap? loaded;
        try
        {
            loaded = File.Exists(executablePath) ? Extract(executablePath, 0, pixelSize) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }

        if (loaded is not null)
        {
            Cache[key] = loaded;
        }

        return loaded;
    }

    private static Bitmap? Load(string providerId, int pixelSize)
    {
        var location = providerId switch
        {
            SyncProviderCatalog.OneDrive => LocateOneDrive(),
            SyncProviderCatalog.Syncthing => LocateRunningProcess("syncthing"),
            SyncProviderCatalog.GlobalSecureAccess => LocateRunningProcess("GlobalSecureAccessClient"),
            _ => null
        };

        return location is null ? null : Extract(location.Value.Path, location.Value.Index, pixelSize);
    }

    private static Bitmap? Extract(string path, int index, int pixelSize)
    {
        var large = IntPtr.Zero;
        var small = IntPtr.Zero;
        try
        {
            // The low word requests the large icon size and the high word the small one.
            var requested = (uint)(pixelSize | (16 << 16));
            if (SHDefExtractIconW(path, index, 0, out large, out small, requested) != 0 || large == IntPtr.Zero)
            {
                return null;
            }

            // Copied off the shared icon so the bitmap outlives the handle destroyed below.
            using var icon = Icon.FromHandle(large);
            return icon.ToBitmap();
        }
        catch (Exception error) when (error is COMException or ArgumentException or DllNotFoundException)
        {
            return null;
        }
        finally
        {
            if (large != IntPtr.Zero)
            {
                DestroyIcon(large);
            }

            if (small != IntPtr.Zero)
            {
                DestroyIcon(small);
            }
        }
    }

    private static (string Path, int Index)? LocateOneDrive()
    {
        var registered = OneDriveIconFromRegistry();
        if (registered is not null)
        {
            return registered;
        }

        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft OneDrive", "OneDrive.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft OneDrive", "OneDrive.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "OneDrive", "OneDrive.exe")
        ];

        var found = candidates.FirstOrDefault(File.Exists);
        return found is null ? null : (found, 0);
    }

    private static (string Path, int Index)? OneDriveIconFromRegistry()
    {
        try
        {
            using var manager = Registry.LocalMachine.OpenSubKey(SyncRootManagerKey);
            if (manager is null)
            {
                return null;
            }

            foreach (var name in manager.GetSubKeyNames())
            {
                if (!name.StartsWith("OneDrive", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using var root = manager.OpenSubKey(name);
                if (root?.GetValue("IconResource") is string resource)
                {
                    return ParseIconResource(resource);
                }
            }
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }

        return null;
    }

    /// <summary>Shell icon references look like "C:\path\app.exe,-501".</summary>
    private static (string Path, int Index)? ParseIconResource(string resource)
    {
        var separator = resource.LastIndexOf(',');
        if (separator < 0)
        {
            return File.Exists(resource) ? (resource, 0) : null;
        }

        var path = resource[..separator].Trim().Trim('"');
        if (!File.Exists(path) || !int.TryParse(resource[(separator + 1)..].Trim(), out var index))
        {
            return null;
        }

        return (path, index);
    }

    private static (string Path, int Index)? LocateRunningProcess(string processName)
    {
        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                var file = process.MainModule?.FileName;
                if (file is not null && File.Exists(file))
                {
                    return (file, 0);
                }
            }
            catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                // A process this build cannot open simply contributes no icon.
            }
            finally
            {
                process.Dispose();
            }
        }

        return null;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHDefExtractIconW(
        string iconFile, int index, uint flags, out IntPtr largeIcon, out IntPtr smallIcon, uint iconSize);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
