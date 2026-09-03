// Process entry point.
using System.Windows.Forms;

namespace JKBar.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new JkBarContext());
    }
}
