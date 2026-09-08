// Ported from JKMon.Core/Presentation/FullscreenGate.cs; the bar hides for the same reasons the overlay did.
using JKBar.Core.Layout;

namespace JKBar.Core.Presentation;

/// <summary>Values of SHQueryUserNotificationState, which is how Windows reports that something owns the screen.</summary>
public enum UserNotificationState
{
    Unknown = 0,
    NotPresent = 1,
    Busy = 2,
    RunningDirect3DFullScreen = 3,
    PresentationMode = 4,
    AcceptsNotifications = 5,
    QuietTime = 6,
    RunningWindowsStoreApp = 7
}

/// <summary>
/// Decides whether a full-screen application owns the display. The bar then stops drawing and the app stops
/// sampling, because a video or a game is exactly when the cost of both is least welcome.
/// </summary>
public static class FullscreenGate
{
    public static bool CoversMonitor(NotchGeometry.Rect window, NotchGeometry.Rect monitor) =>
        monitor.Width > 0 && monitor.Height > 0 &&
        window.Left <= monitor.Left && window.Top <= monitor.Top &&
        window.Right >= monitor.Right && window.Bottom >= monitor.Bottom;

    /// <summary>
    /// Two signals, because neither alone is enough. Windows reports exclusive full-screen and presentation mode
    /// directly, but says nothing about the borderless windows most players and games actually use, which is what
    /// the geometry test catches.
    ///
    /// <paramref name="foregroundSharesBarMonitor"/> is what keeps a second monitor usable. Windows reports
    /// full-screen for the whole session, not per display, so a game on one screen would otherwise blank the bar
    /// on another. Only the display the bar actually sits on can hide it.
    ///
    /// <paramref name="foregroundIsShell"/> excludes the desktop and the taskbar. They cover the monitor by nature,
    /// and alt-tabbing out of a game to the desktop is exactly when the bar should come back.
    /// </summary>
    public static bool ShouldSuppress(
        bool enabled,
        UserNotificationState state,
        bool foregroundIsShell,
        bool foregroundSharesBarMonitor,
        NotchGeometry.Rect foreground,
        NotchGeometry.Rect monitor)
    {
        if (!enabled || foregroundIsShell || !foregroundSharesBarMonitor)
        {
            return false;
        }

        if (state is UserNotificationState.Busy
            or UserNotificationState.RunningDirect3DFullScreen
            or UserNotificationState.PresentationMode)
        {
            return true;
        }

        return CoversMonitor(foreground, monitor);
    }
}
