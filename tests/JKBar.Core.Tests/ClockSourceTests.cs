// Pins the clock's repaint signature, which is what stops a full-width surface being redrawn once a second.
using System.Globalization;
using System.Drawing;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;

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
        Assert.Equal("WWW 30", yardstick);

        for (var day = 0; day < 366; day++)
        {
            var label = ClockSource.Item(Noon.AddDays(day), CultureInfo.InvariantCulture).Label;
            Assert.True(label.Length <= yardstick.Length, $"'{label}' is longer than the yardstick '{yardstick}'");
        }
    }

    [Theory]
    [InlineData(FontStyle.Regular)]
    [InlineData(FontStyle.Bold)]
    [InlineData(FontStyle.Italic)]
    [InlineData(FontStyle.Bold | FontStyle.Italic)]
    public void DateYardstickIsWiderThanEveryRenderedLabel(FontStyle style)
    {
        using var bitmap = new Bitmap(1, 1);
        bitmap.SetResolution(168, 168);
        using var graphics = Graphics.FromImage(bitmap);
        using var font = new Font(BandTypographySettings.DefaultFontFamily, 16, style, GraphicsUnit.Pixel);
        using var format = StringFormat.GenericTypographic;
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
        var yardstick = ClockSource.Item(Noon, CultureInfo.InvariantCulture).LabelYardstick;
        var yardstickWidth = Measure(graphics, yardstick, font, format);

        for (var day = 0; day < 366; day++)
        {
            var label = ClockSource.Item(Noon.AddDays(day), CultureInfo.InvariantCulture).Label;
            Assert.True(
                Measure(graphics, label, font, format) <= yardstickWidth,
                $"'{label}' renders wider than the yardstick '{yardstick}' for {style}");
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

    private static int Measure(Graphics graphics, string text, Font font, StringFormat format) =>
        (int)Math.Ceiling(graphics.MeasureString(text, font, PointF.Empty, format).Width);
}
