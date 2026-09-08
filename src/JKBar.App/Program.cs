// Process entry point.
using System.Windows.Forms;
using JKBar.App.Diagnostics;
using JKBar.App.Update;

namespace JKBar.App;

internal static class Program
{
    [STAThread]
    private static int Main(string[] arguments)
    {
        // Started by the previous build to replace it, so no window opens and no settings are read.
        if (UpdateArguments.Parse(arguments) is { } update)
        {
            return UpdateApplier.Run(update);
        }

        UpdateDownloader.TryDelete(UpdateArguments.CleanupTarget(arguments));

        ApplicationConfiguration.Initialize();
        CrashLog.Install();
        Application.Run(new JkBarContext());

        return 0;
    }
}
