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

        Assert.Equal("12:34", Assert.Single(item.Values).Text);
        Assert.Equal(BandItemKind.Clock, item.Kind);
    }

    [Fact]
    public void LabelsTheDayInEnglishWithoutTheMonth()
    {
        var label = ClockSource.Item(Noon, new CultureInfo("ko-KR")).Label;

        Assert.Equal("Thu 3", label);
    }

    /// <summary>
    /// The clock sits at the right-hand end, so its date label changing width would push every other reading
    /// along. The yardstick has to be at least as wide as any date the format can produce.
    /// </summary>
    [Fact]
    public void SizesTheDateLabelForTheLongestNames()
    {
        var yardstick = ClockSource.Item(Noon, CultureInfo.InvariantCulture).LabelYardstick;

        for (var day = 0; day < 366; day++)
        {
            var label = ClockSource.Item(Noon.AddDays(day), CultureInfo.InvariantCulture).Label;
            Assert.True(label.Length <= yardstick.Length, $"'{label}' is longer than the yardstick '{yardstick}'");
        }
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
