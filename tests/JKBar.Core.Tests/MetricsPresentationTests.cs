// Formatting decides how often the whole band repaints, so the unit boundaries and the signature are pinned.
using JKBar.Core.Metrics;
using JKBar.Core.Presentation;

namespace JKBar.Core.Tests;

public class MetricsPresentationTests
{
    [Theory]
    [InlineData(0, "0 B/s")]
    [InlineData(1023, "1023 B/s")]
    [InlineData(1024, "1 KB/s")]
    [InlineData(1024 * 1024 - 1, "1024 KB/s")]
    [InlineData(1024 * 1024, "1.0 MB/s")]
    [InlineData(1024d * 1024 * 1024, "1.0 GB/s")]
    public void FormatsEachUnitBand(double bytesPerSecond, string expected)
    {
        Assert.Equal(expected, ByteRate.PerSecond(bytesPerSecond));
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
        var barelyDifferent = new MetricsSnapshot(30.4, 60.3, 2100, 1080, 0, 0);

        Assert.Equal(MetricsSource.Signature(quiet), MetricsSource.Signature(barelyDifferent));
    }

    [Fact]
    public void ChangesSignatureWhenAReadoutChanges()
    {
        var quiet = new MetricsSnapshot(30, 60, 0, 0, 0, 0);
        var busy = new MetricsSnapshot(85, 60, 0, 0, 0, 0);

        Assert.NotEqual(MetricsSource.Signature(quiet), MetricsSource.Signature(busy));
    }
}
