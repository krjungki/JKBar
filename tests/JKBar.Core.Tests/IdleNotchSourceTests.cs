// Pins the resting-notch text and its minute-level repaint boundary.
using System.Globalization;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;

namespace JKBar.Core.Tests;

public class IdleNotchSourceTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 7, 12, 34, 56, TimeSpan.Zero);

    [Fact]
    public void ShowsDateAndTimeOnOneLine()
    {
        Assert.Equal("Mon 7  12:34", IdleNotchSource.Text(Noon, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void KeepsTheSameSignatureWithinAMinute()
    {
        Assert.Equal(IdleNotchSource.Signature(Noon), IdleNotchSource.Signature(Noon.AddSeconds(3)));
    }

    [Fact]
    public void ChangesSignatureAtTheNextMinute()
    {
        Assert.NotEqual(IdleNotchSource.Signature(Noon), IdleNotchSource.Signature(Noon.AddMinutes(1)));
    }

    [Fact]
    public void LeavesTheRestingNotchEmptyWhenConfigured()
    {
        Assert.Null(IdleNotchSource.Content(
            IdleNotchContent.Empty,
            hasActiveAlert: false,
            Noon,
            CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ActiveAlertOverridesRestingDateAndTime()
    {
        Assert.Null(IdleNotchSource.Content(
            IdleNotchContent.DateTime,
            hasActiveAlert: true,
            Noon,
            CultureInfo.InvariantCulture));
    }
}