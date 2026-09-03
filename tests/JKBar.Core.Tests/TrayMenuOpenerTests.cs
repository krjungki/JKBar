// Guards the one non-public API the app leans on: without it, left-clicking the tray icon would quietly do nothing.
using System.Reflection;
using System.Windows.Forms;

namespace JKBar.Core.Tests;

public class TrayMenuOpenerTests
{
    /// <summary>
    /// NotifyIcon opens its menu on right-click but offers no public equivalent, so JkBarContext calls this
    /// method to give the left button identical behaviour. A runtime that renames it must fail here rather
    /// than in the user's hands.
    /// </summary>
    [Fact]
    public void NotifyIconStillExposesShowContextMenu()
    {
        var opener = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(opener);
        Assert.Empty(opener.GetParameters());
    }
}
