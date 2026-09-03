// Pins the clock's repaint signature, which is what stops a full-width surface being redrawn once a second.
using System.Globalization;
using JKBar.Core.Presentation;

namespace JKBar.Core.Tests;

public class ClockSourceTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 3, 12, 34, 56, TimeSpan.Zero);

    [Fact]
    public void ShowsTheTimeAsTheValue()
    {
        var item = ClockSource.Item(Noon, CultureInfo.InvariantCulture);

        Assert.Equal("12:34", item.Value);
    }

    [Fact]
    public void HoldsTheSameSignatureAcrossAMinute()
    {
        Assert.Equal(ClockSource.Signature(Noon), ClockSource.Signature(Noon.AddSeconds(3)));
    }

    [Fact]
    public void ChangesSignatureOnTheNextMinute()
    {
        Assert.NotEqual(ClockSource.Signature(Noon), ClockSource.Signature(Noon.AddMinutes(1)));
    }
}
