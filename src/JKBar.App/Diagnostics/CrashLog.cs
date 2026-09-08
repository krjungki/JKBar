// Records faults that would otherwise vanish with the process, so a brief flash on screen leaves a trace.
using System.Text;
using JKBar.Core.Settings;
using IoPath = System.IO.Path;

namespace JKBar.App.Diagnostics;

internal static class CrashLog
{
    private const string FileName = "errors.log";
    private const long SizeLimit = 256 * 1024;

    private static readonly Lock Gate = new();

    private static string? _folder;

    /// <summary>Where faults are written, chosen once so a later failure cannot pick a different file.</summary>
    public static string Path => IoPath.Combine(_folder ??= SettingsStore.ForApp().Folder, FileName);

    /// <summary>Catches the faults that WinForms would otherwise answer with a dialog nobody can read.</summary>
    public static void Install()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Write("화면 스레드", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write("배경 스레드", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write("끝나지 않은 작업", e.Exception);
            e.SetObserved();
        };
    }

    public static void Write(string origin, Exception? error)
    {
        if (error is null)
        {
            return;
        }

        var entry = new StringBuilder()
            .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            .Append(' ')
            .Append(origin)
            .AppendLine()
            .AppendLine(error.ToString())
            .AppendLine()
            .ToString();

        try
        {
            lock (Gate)
            {
                var file = Path;
                if (new FileInfo(file) is { Exists: true, Length: > SizeLimit })
                {
                    File.Delete(file);
                }

                File.AppendAllText(file, entry, Encoding.UTF8);
            }
        }
        catch (IOException)
        {
            // A log that cannot be written must not become the next fault.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
