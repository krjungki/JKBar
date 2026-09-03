// Formatting decides how often the whole band repaints, so the unit boundaries and the signature are pinned.
using JKBar.Core.Metrics;
using JKBar.Core.Presentation;

namespace JKBar.Core.Tests;

public class MetricsPresentationTests
{
    [Theory]
    [InlineData(0, "0 B/s")]
    [InlineData(1023, "1023 B/s")]
    [InlineData(1024, "1.0 KB/s")]
    [InlineData(1024 * 12, "12 KB/s")]
    [InlineData((1024 * 1024) - 1, "1024 KB/s")]
    [InlineData(1024 * 1024, "1.0 MB/s")]
    [InlineData(1024d * 1024 * 1024, "1.0 GB/s")]
    public void FormatsEachUnitBand(double bytesPerSecond, string expected)
    {
        Assert.Equal(expected, ByteRate.PerSecond(bytesPerSecond));
    }

    /// <summary>
    /// Layout reserves the template's width. Anything wider would spill into the neighbouring reading and bring
    /// back the shifting this replaced.
    /// </summary>
    [Fact]
    public void NeverFormatsWiderThanItsTemplate()
    {
        double[] rates = [0, 1, 1023, 1024, 999_999, 1024 * 1024, 900d * 1024 * 1024, 1024d * 1024 * 1024, 9e14];

        Assert.All(rates, rate =>
            Assert.True(
                ByteRate.PerSecond(rate).Length <= ByteRate.WidestRate.Length,
                $"{rate} formatted as '{ByteRate.PerSecond(rate)}', wider than '{ByteRate.WidestRate}'"));
    }

    [Fact]
    public void NeverFormatsAPercentWiderThanItsTemplate()
    {
        double[] percents = [0, 9, 61.4, 100, 140];

        Assert.All(percents, percent =>
            Assert.True(ByteRate.Percent(percent).Length <= ByteRate.WidestPercent.Length));
    }

    /// <summary>A counter failure can hand back NaN, which must not reach the band as "NaN KB/s".</summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-5000)]
    public void RefusesValuesThatAreNotRates(double bytesPerSecond)
    {
        Assert.Equal("0 B/s", ByteRate.PerSecond(bytesPerSecond));
    }

    [Theory]
    [InlineData(0, "0%")]
    [InlineData(61.4, "61%")]
    [InlineData(140, "100%")]
    [InlineData(double.NaN, "0%")]
    public void ClampsPercentages(double percent, string expected)
    {
        Assert.Equal(expected, ByteRate.Percent(percent));
    }

    [Fact]
    public void ListsTheReadoutsInDisplayOrder()
    {
        var labels = MetricsSource.Items(MetricsSnapshot.Empty).Select(item => item.Label);

        Assert.Equal(["CPU", "RAM", "DISK", "NET"], labels);
    }

    /// <summary>Sub-unit jitter must not force a full screen-width repaint every second.</summary>
    [Fact]
    public void HoldsItsSignatureThroughChangeTooSmallToShow()
    {
        var quiet = new MetricsSnapshot(30.2, 60.1, 2048, 1024, 0, 0);
        var barelyDifferent = new MetricsSnapshot(30.4, 60.3, 2050, 1026, 0, 0);

        Assert.Equal(MetricsSource.Signature(quiet), MetricsSource.Signature(barelyDifferent));
    }

    /// <summary>Each rate gets its own box so the arrow beside it does not slide as the number grows.</summary>
    [Fact]
    public void GivesThroughputTwoSeparateValues()
    {
        var network = MetricsSource.Items(MetricsSnapshot.Empty).Single(item => item.Label == "NET");

        Assert.Equal(2, network.Values.Count);
        Assert.All(network.Values, value => Assert.False(string.IsNullOrEmpty(value.Template)));
    }

    [Fact]
    public void ChangesSignatureWhenAReadoutChanges()
    {
        var quiet = new MetricsSnapshot(30, 60, 0, 0, 0, 0);
        var busy = new MetricsSnapshot(85, 60, 0, 0, 0, 0);

        Assert.NotEqual(MetricsSource.Signature(quiet), MetricsSource.Signature(busy));
    }
}
