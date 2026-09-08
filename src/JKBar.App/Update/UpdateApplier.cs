// Ported from JKMon (packages/JKMon/src/JKMon.App/Update/UpdateApplier.cs). Keep behaviour changes in sync.
using System.Diagnostics;
using JKBar.Core.Update;
using IoPath = System.IO.Path;

namespace JKBar.App.Update;

/// <summary>
/// Runs inside the freshly downloaded copy, not the installed one, so the files it replaces are not the files it
/// is running from. Only what the release ships is touched: settings and logs stay where the user left them.
/// </summary>
internal static class UpdateApplier
{
    private const string BackupFolderName = ".jkbar-previous";
    private static readonly TimeSpan GracefulExitTimeout = TimeSpan.FromSeconds(10);

    internal static int Run(UpdateArguments arguments)
    {
        var source = arguments.SourceDirectory!;
        var target = arguments.TargetDirectory!;
        var backup = IoPath.Combine(target, BackupFolderName);

        if (!WaitForExit(arguments.WaitForProcessId))
        {
            return 2;
        }

        try
        {
            Erase(backup);
            Directory.CreateDirectory(backup);

            foreach (var incoming in Directory.GetFiles(source))
            {
                var name = IoPath.GetFileName(incoming);
                var existing = IoPath.Combine(target, name);
                if (File.Exists(existing))
                {
                    File.Move(existing, IoPath.Combine(backup, name), overwrite: true);
                }

                File.Copy(incoming, existing, overwrite: true);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Rollback(backup, target);
            return 3;
        }

        if (!Relaunch(IoPath.Combine(target, UpdateDownloader.ExecutableName), arguments.WorkDirectory))
        {
            Rollback(backup, target);
            Relaunch(IoPath.Combine(target, UpdateDownloader.ExecutableName), null);
            return 4;
        }

        Erase(backup);

        return 0;
    }

    private static bool WaitForExit(int processId)
    {
        if (processId <= 0)
        {
            return true;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.WaitForExit((int)GracefulExitTimeout.TotalMilliseconds))
            {
                return true;
            }

            process.Kill(entireProcessTree: true);

            return process.WaitForExit((int)GracefulExitTimeout.TotalMilliseconds);
        }
        catch (ArgumentException)
        {
            // Already gone, which is what was being waited for.
            return true;
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static bool Relaunch(string executable, string? workDirectory)
    {
        try
        {
            var info = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                WorkingDirectory = IoPath.GetDirectoryName(executable)!
            };

            // The new copy deletes the staging folder, because this process is running out of it.
            if (StagingPaths.IsStagingRoot(workDirectory))
            {
                info.ArgumentList.Add(UpdateArguments.CleanupSwitch);
                info.ArgumentList.Add(workDirectory!);
            }

            using var started = Process.Start(info);

            return started is not null;
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception
            or InvalidOperationException or IOException)
        {
            return false;
        }
    }

    private static void Rollback(string backup, string target)
    {
        try
        {
            if (!Directory.Exists(backup))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(backup))
            {
                File.Move(file, IoPath.Combine(target, IoPath.GetFileName(file)), overwrite: true);
            }

            Directory.Delete(backup, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Nothing further can be done from here; the backup folder stays for manual recovery.
        }
    }

    private static void Erase(string backup)
    {
        try
        {
            if (Directory.Exists(backup))
            {
                Directory.Delete(backup, recursive: true);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The folder only holds the previous build; leaving it costs nothing but disk.
        }
    }
}
