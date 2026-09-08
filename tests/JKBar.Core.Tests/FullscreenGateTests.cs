// Ported from JKMon.Core.Tests/FullscreenGateTests.cs.
using JKBar.Core.Layout;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;

namespace JKBar.Core.Tests;

public class FullscreenGateTests
{
    private static readonly NotchGeometry.Rect Monitor = new(0, 0, 2560, 1440);
    private static readonly NotchGeometry.Rect Fullscreen = new(0, 0, 2560, 1440);
    private static readonly NotchGeometry.Rect Windowed = new(100, 100, 1200, 800);

    private const UserNotificationState Normal = UserNotificationState.AcceptsNotifications;

    /// <summary>The common case: one display, so the foreground always shares it with the bar.</summary>
    private static bool Suppress(
        UserNotificationState state,
        NotchGeometry.Rect foreground,
        bool isShell = false,
        bool sharesMonitor = true,
        bool enabled = true) =>
        FullscreenGate.ShouldSuppress(enabled, state, isShell, sharesMonitor, foreground, Monitor);

    [Fact]
    public void SuppressesABorderlessWindowThatCoversTheMonitor()
    {
        Assert.True(Suppress(Normal, Fullscreen));
    }

    /// <summary>A game that goes past the monitor edges still owns the screen.</summary>
    [Fact]
    public void SuppressesAWindowLargerThanTheMonitor()
    {
        Assert.True(Suppress(Normal, new NotchGeometry.Rect(-8, -8, 2600, 1500)));
    }

    [Fact]
    public void LeavesAWindowedAppAlone()
    {
        Assert.False(Suppress(Normal, Windowed));
    }

    /// <summary>
    /// The desktop and the taskbar cover the monitor by nature. Counting them would hide the bar whenever the user
    /// clicked the desktop, which is precisely when they want to see it.
    /// </summary>
    [Fact]
    public void NeverSuppressesForTheShell()
    {
        Assert.False(Suppress(Normal, Fullscreen, isShell: true));
    }

    /// <summary>Alt-tabbing from a game to the desktop should bring the bar back even if Windows still says busy.</summary>
    [Fact]
    public void TheShellWinsOverTheNotificationState()
    {
        Assert.False(Suppress(UserNotificationState.RunningDirect3DFullScreen, Fullscreen, isShell: true));
    }

    [Theory]
    [InlineData(UserNotificationState.Busy)]
    [InlineData(UserNotificationState.RunningDirect3DFullScreen)]
    [InlineData(UserNotificationState.PresentationMode)]
    public void TrustsWindowsWhenItReportsSomethingOwnsTheScreen(UserNotificationState state)
    {
        Assert.True(Suppress(state, Windowed));
    }

    /// <summary>
    /// Windows reports full-screen for the session, not per display, so a game on the other monitor must not blank
    /// the bar. This is the case that cannot be checked on a single-monitor machine.
    /// </summary>
    [Theory]
    [InlineData(UserNotificationState.Busy)]
    [InlineData(UserNotificationState.RunningDirect3DFullScreen)]
    [InlineData(UserNotificationState.PresentationMode)]
    public void IgnoresAFullScreenAppOnAnotherMonitor(UserNotificationState state)
    {
        Assert.False(Suppress(state, Fullscreen, sharesMonitor: false));
    }

    [Fact]
    public void IgnoresABorderlessWindowOnAnotherMonitor()
    {
        Assert.False(Suppress(Normal, Fullscreen, sharesMonitor: false));
    }

    /// <summary>
    /// This one fires for any Store app in the foreground, full screen or not, so acting on it would hide the bar
    /// behind an ordinary windowed app.
    /// </summary>
    [Fact]
    public void IgnoresTheStoreAppState()
    {
        Assert.False(Suppress(UserNotificationState.RunningWindowsStoreApp, Windowed));
    }

    [Theory]
    [InlineData(UserNotificationState.Unknown)]
    [InlineData(UserNotificationState.QuietTime)]
    [InlineData(UserNotificationState.NotPresent)]
    public void OtherStatesFallBackToTheGeometry(UserNotificationState state)
    {
        Assert.False(Suppress(state, Windowed));
        Assert.True(Suppress(state, Fullscreen));
    }

    [Fact]
    public void DoesNothingWhenTheSettingIsOff()
    {
        Assert.False(Suppress(UserNotificationState.RunningDirect3DFullScreen, Fullscreen, enabled: false));
    }

    [Fact]
    public void AnEmptyMonitorRectangleIsNotCovered()
    {
        Assert.False(FullscreenGate.CoversMonitor(Fullscreen, default));
    }

    [Fact]
    public void HidingIsOnByDefault()
    {
        Assert.True(new JkBarSettings().Normalized().Behaviour.HideWhenFullscreen);
    }

    [Fact]
    public void HidingCanBeTurnedOff()
    {
        var settings = new JkBarSettings { Behaviour = new BehaviourSettings { HideWhenFullscreen = false } };

        Assert.False(settings.Normalized().Behaviour.HideWhenFullscreen);
    }
}
