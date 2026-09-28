// Covers the clock's configurable parts and the spacing settings that sit beside them.
using System.Globalization;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;

namespace JKBar.Core.Tests;

public class ClockOptionsTests
{
    private static readonly DateTimeOffset Evening = new(2026, 9, 28, 21, 39, 0, TimeSpan.FromHours(9));

    [Fact]
    public void DefaultClockMatchesTheSingleLineItem()
    {
        var item = Assert.Single(ClockSource.Items(Evening, CultureInfo.InvariantCulture, new ClockSettings()));
        var classic = ClockSource.Item(Evening, CultureInfo.InvariantCulture);

        Assert.Equal(classic.Label, item.Label);
        Assert.Equal(classic.LabelYardstick, item.LabelYardstick);
        Assert.Equal(classic.Values, item.Values);
        Assert.Equal(BandItemLayout.Inline, item.Layout);
    }

    [Theory]
    [InlineData(false, true, true, "28", "30")]
    [InlineData(true, false, true, "Mon", "WWW")]
    [InlineData(false, false, true, "", "")]
    public void HiddenDatePartsLeaveTheirYardstickOut(bool weekday, bool day, bool time, string label, string yardstick)
    {
        var item = Assert.Single(ClockSource.Items(Evening, CultureInfo.InvariantCulture,
            new ClockSettings { ShowWeekday = weekday, ShowDay = day, ShowTime = time }));

        Assert.Equal(label, item.Label);
        Assert.Equal(yardstick, item.LabelYardstick);
        Assert.Equal("21:39", Assert.Single(item.Values).Text);
    }

    [Fact]
    public void HidingTheTimeLeavesOnlyTheDate()
    {
        var item = Assert.Single(ClockSource.Items(Evening, CultureInfo.InvariantCulture,
            new ClockSettings { ShowTime = false, TwoLines = true }));

        Assert.Equal("Mon 28", item.Label);
        Assert.Empty(item.Values);
        Assert.Equal(BandItemLayout.Inline, item.Layout);
    }

    [Fact]
    public void HidingEveryPartRemovesTheClock()
    {
        Assert.Empty(ClockSource.Items(Evening, CultureInfo.InvariantCulture,
            new ClockSettings { ShowWeekday = false, ShowDay = false, ShowTime = false }));
    }

    [Fact]
    public void TwoLinesPutsTheTimeOverTheDate()
    {
        var item = Assert.Single(ClockSource.Items(Evening, CultureInfo.InvariantCulture, new ClockSettings { TwoLines = true }));

        Assert.Equal(BandItemLayout.ClockRows, item.Layout);
        Assert.Equal(BandItemKind.Clock, item.Kind);
        Assert.Equal(["21:39", "Mon 28"], item.Values.Select(value => value.Text));
        Assert.Equal(["00:00", "WWW 30"], item.Values.Select(value => value.Template));
    }

    [Fact]
    public void TwoLinesWithoutADateStaysOnOneLine()
    {
        var item = Assert.Single(ClockSource.Items(Evening, CultureInfo.InvariantCulture,
            new ClockSettings { ShowWeekday = false, ShowDay = false, TwoLines = true }));

        Assert.Equal(BandItemLayout.Inline, item.Layout);
        Assert.Equal("21:39", Assert.Single(item.Values).Text);
    }

    [Fact]
    public void SpacingAndClockDefaultClampAndRoundTrip()
    {
        var defaults = new BandItemsSettings().Normalized();
        Assert.Equal(100, defaults.MetricIconSpacingPercent);
        Assert.Equal(new ClockSettings(), defaults.Clock);

        Assert.Equal(0, new BandItemsSettings { MetricIconSpacingPercent = -20 }.Normalized().MetricIconSpacingPercent);
        var clamped = new BandItemsSettings { MetricIconSpacingPercent = 900, Clock = null! }.Normalized();
        Assert.Equal(BandItemsSettings.MaximumSpacingPercent, clamped.MetricIconSpacingPercent);
        Assert.Equal(new ClockSettings(), clamped.Clock);

        var path = Path.Combine(Path.GetTempPath(), $"jkbar-clock-{Guid.NewGuid():N}", "settings.json");
        try
        {
            var store = new SettingsStore(path);
            var expected = new BandItemsSettings
            {
                MetricIconSpacingPercent = 250,
                Clock = new ClockSettings { ShowWeekday = false, TwoLines = true }
            };
            store.Save(new JkBarSettings { BandItems = expected });
            var loaded = store.Load().BandItems;

            Assert.Equal(250, loaded.MetricIconSpacingPercent);
            Assert.Equal(expected.Clock, loaded.Clock);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void OldTypographyMovesOntoTheBundledFontOnce()
    {
        var old = new BandTypographySettings
        {
            FontFamily = "Segoe UI",
            FontSizePercent = 38,
            Bold = true,
            Italic = true,
            TextShadow = true,
            TextColourArgb = unchecked((int)0xFFFAFAFA)
        };

        var migrated = old.Migrated();

        Assert.Equal("Pretendard SemiBold", migrated.FontFamily);
        Assert.Equal(42, migrated.FontSizePercent);
        Assert.False(migrated.Bold);
        Assert.False(migrated.Italic);
        Assert.True(migrated.TextShadow);
        Assert.Equal(old.TextColourArgb, migrated.TextColourArgb);
        Assert.Equal(BandTypographySettings.CurrentFontRevision, migrated.FontRevision);

        var chosenLater = migrated with { FontFamily = "Malgun Gothic", Bold = true };
        Assert.Same(chosenLater, chosenLater.Migrated());
    }

    [Fact]
    public void SettingsWrittenBeforeTheOptionsKeepTheOldLook()
    {
        var path = Path.Combine(Path.GetTempPath(), $"jkbar-clock-{Guid.NewGuid():N}", "settings.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, """{ "BandItems": { "Order": [ 6 ] } }""");

            var loaded = new SettingsStore(path).Load().BandItems;

            Assert.Equal(100, loaded.MetricIconSpacingPercent);
            Assert.Equal(new ClockSettings(), loaded.Clock);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
