// Asks a packaged application to open itself, which is the only way in for an executable under WindowsApps.
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace JKBar.App.Interop;

[SupportedOSPlatform("windows")]
internal static class PackagedApp
{
    private const uint QueryLimitedInformation = 0x1000;
    private const int Success = 0;
    private const int InsufficientBuffer = 122;

    /// <summary>The application user model id of a running packaged process, or null for an ordinary one.</summary>
    internal static string? UserModelIdOf(int processId)
    {
        var handle = OpenProcess(QueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            uint length = 0;
            if (GetApplicationUserModelId(handle, ref length, null) != InsufficientBuffer || length == 0)
            {
                return null;
            }

            var buffer = new StringBuilder((int)length);
            return GetApplicationUserModelId(handle, ref length, buffer) == Success ? buffer.ToString() : null;
        }
        catch (Exception error) when (error is EntryPointNotFoundException or DllNotFoundException)
        {
            return null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    /// <summary>
    /// Launching goes through the shell's app folder rather than the executable: the file itself cannot be run
    /// directly. A packaged app that is already running takes this as the request to show its window.
    /// </summary>
    internal static bool Open(string userModelId)
    {
        if (!IsSafeIdentifier(userModelId))
        {
            return false;
        }

        try
        {
            using var started = Process.Start(new ProcessStartInfo("explorer.exe")
            {
                // Passed as one argument so nothing in the identifier can be read as a second one.
                ArgumentList = { $"shell:AppsFolder\\{userModelId}" },
                UseShellExecute = false
            });

            return true;
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or IOException
            or ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>The identifier comes from Windows, and this keeps anything else from reaching the shell.</summary>
    private static bool IsSafeIdentifier(string value) =>
        value.Length is > 0 and <= 260
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-' or '!');

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetApplicationUserModelId(IntPtr process, ref uint length, StringBuilder? id);
}
