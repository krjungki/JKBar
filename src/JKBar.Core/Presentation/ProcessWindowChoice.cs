// Picks which of an application's top-level windows to raise when its icon in the band is clicked.
namespace JKBar.Core.Presentation;

public static class ProcessWindowChoice
{
    public const int Unusable = 0;

    /// <summary>An application that was sent to the tray keeps its window; it is only hidden.</summary>
    public const int HiddenAppWindow = 1;

    public const int VisibleAppWindow = 2;

    /// <param name="owned">True when another window owns it, which marks a dialog or popup rather than the app.</param>
    /// <param name="appFrame">
    /// True when the window carries a system menu. Helper windows a toolkit keeps around for tray messages and
    /// media keys are titled and unowned like a real window, and this is what tells them apart.
    /// </param>
    public static int Score(bool titled, bool owned, bool toolWindow, bool appFrame, bool visible)
    {
        if (!titled || owned || toolWindow || !appFrame)
        {
            return Unusable;
        }

        return visible ? VisibleAppWindow : HiddenAppWindow;
    }
}
