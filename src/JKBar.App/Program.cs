// Process entry point. The bar window and tray arrive in later phases; this only proves the app host builds.
using System.Windows.Forms;

namespace JKBar.App;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();
        return 0;
    }
}
