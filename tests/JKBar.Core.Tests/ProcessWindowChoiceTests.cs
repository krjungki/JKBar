// Regression checks for choosing which window a clicked process icon should raise.
using JKBar.Core.Presentation;

namespace JKBar.Core.Tests;

public class ProcessWindowChoiceTests
{
    private static int Score(bool titled = true, bool owned = false, bool toolWindow = false,
        bool appFrame = true, bool visible = true) =>
        ProcessWindowChoice.Score(titled, owned, toolWindow, appFrame, visible);

    [Fact]
    public void PrefersAWindowThatIsAlreadyOnScreen()
    {
        Assert.True(Score(visible: true) > Score(visible: false));
        Assert.Equal(ProcessWindowChoice.VisibleAppWindow, Score(visible: true));
    }

    /// <summary>
    /// Measured on Telegram: raising a window the application had hidden on its way to the tray put up a frame
    /// the application never drew into, so only a window that is already on screen counts as one to raise.
    /// </summary>
    [Fact]
    public void AWindowTheApplicationHidIsNotOneToRaise()
    {
        Assert.NotEqual(ProcessWindowChoice.VisibleAppWindow, Score(visible: false));
        Assert.Equal(ProcessWindowChoice.HiddenAppWindow, Score(visible: false));
    }

    /// <summary>
    /// Measured on Telegram: its tray helper window is titled `QTrayIconMessageWindow`, is unowned and is not a
    /// tool window, but carries no system menu. Showing that one put an empty window on screen.
    /// </summary>
    [Fact]
    public void SkipsAToolkitHelperWindowThatOnlyLooksLikeAnAppWindow()
    {
        Assert.Equal(ProcessWindowChoice.Unusable, Score(appFrame: false, visible: false));
        Assert.Equal(ProcessWindowChoice.Unusable, Score(appFrame: false, visible: true));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void SkipsDialogsHelpersAndUntitledWindows(bool titled, bool owned, bool toolWindow)
    {
        Assert.Equal(ProcessWindowChoice.Unusable, Score(titled, owned, toolWindow, visible: true));
        Assert.Equal(ProcessWindowChoice.Unusable, Score(titled, owned, toolWindow, visible: false));
    }
}
