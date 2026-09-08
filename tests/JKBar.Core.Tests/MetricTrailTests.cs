using JKBar.Core.Metrics;
using JKBar.Core.Presentation;
using JKBar.Core.Settings;

namespace JKBar.Core.Tests;

public sealed class MetricTrailTests
{
    [Fact]
    public void ReturnsNothingBeforeTheFirstReading()
    {
        Assert.Empty(new MetricTrail().Readings());
    }

    [Fact]
    public void KeepsReadingsInTheOrderTheyArrived()
    {
        var trail = new MetricTrail(4);
        trail.Add(10);
        trail.Add(20);
        trail.Add(30);

        Assert.Equal([10d, 20d, 30d], trail.Readings());
    }

    [Fact]
    public void DropsTheOldestOnceItIsFull()
    {
        var trail = new MetricTrail(3);
        foreach (var reading in new double[] { 1, 2, 3, 4, 5 })
        {
            trail.Add(reading);
        }

        Assert.Equal([3d, 4d, 5d], trail.Readings());
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(140, 100)]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    public void KeepsEveryReadingOnTheChart(double reading, double expected)
    {
        var trail = new MetricTrail(2);
        trail.Add(reading);

        Assert.Equal(expected, trail.Readings()[0]);
    }

    [Fact]
    public void RecordsOnlyTheReadoutsThatCanBeGraphed()
    {
        var trails = new MetricTrails();
        trails.Observe(new MetricsSnapshot(41, 62, 0, 0, 0, 0));

        Assert.Equal([41d], trails.For(BandItemKind.Cpu));
        Assert.Equal([62d], trails.For(BandItemKind.Memory));
        Assert.Empty(trails.For(BandItemKind.Gpu));
        Assert.Empty(trails.For(BandItemKind.Network));
    }

    [Fact]
    public void RecordsTheGraphicsCardOnlyWhileItIsReadable()
    {
        var trails = new MetricTrails();
        trails.Observe(new MetricsSnapshot(0, 0, 0, 0, 0, 0));
        trails.Observe(new MetricsSnapshot(0, 0, 0, 0, 0, 0, GpuPercent: 12, GpuAvailable: true));

        Assert.Equal([12d], trails.For(BandItemKind.Gpu));
    }

    [Fact]
    public void CarriesTheTrailOnEveryPercentageReadout()
    {
        var trails = new MetricTrails();
        trails.Observe(new MetricsSnapshot(30, 40, 0, 0, 0, 0, GpuPercent: 50, GpuAvailable: true));

        var items = MetricsSource.Items(
            new MetricsSnapshot(30, 40, 0, 0, 0, 0, GpuPercent: 50, GpuAvailable: true),
            trails);

        Assert.Equal([30d], items.Single(item => item.Kind == BandItemKind.Cpu).Trail);
        Assert.Equal([50d], items.Single(item => item.Kind == BandItemKind.Gpu).Trail);
        Assert.Equal([40d], items.Single(item => item.Kind == BandItemKind.Memory).Trail);
        Assert.Null(items.Single(item => item.Kind == BandItemKind.Network).Trail);
    }

    [Fact]
    public void LeavesTheTrailOutWhenNoneIsKept()
    {
        Assert.All(
            MetricsSource.Items(MetricsSnapshot.Empty),
            item => Assert.Null(item.Trail));
    }
}

public sealed class BandPercentStyleTests
{
    [Fact]
    public void DrawsNumbersUntilAStyleIsChosen()
    {
        var settings = new BandItemsSettings();

        Assert.Equal(BandPercentStyle.LabelAndValue, settings.StyleFor(BandItemKind.Cpu));
        Assert.All(
            settings.Apply(MetricsSource.Items(MetricsSnapshot.Empty)).Take(2),
            item => Assert.Equal(BandItemLayout.StackedPercent, item.Layout));
    }

    [Theory]
    [InlineData(BandPercentStyle.VerticalLabelGraph, BandItemLayout.VerticalLabelGraph)]
    [InlineData(BandPercentStyle.VerticalLabelValueGraph, BandItemLayout.VerticalLabelValueGraph)]
    public void LaysOutTheReadoutTheChosenWay(BandPercentStyle style, BandItemLayout expected)
    {
        var settings = new BandItemsSettings
        {
            PercentStyles = [new BandItemStyle { Kind = BandItemKind.Cpu, Style = style }]
        };

        var items = settings.Apply(MetricsSource.Items(MetricsSnapshot.Empty));

        Assert.Equal(expected, items.Single(item => item.Kind == BandItemKind.Cpu).Layout);
        Assert.Equal(
            BandItemLayout.StackedPercent,
            items.Single(item => item.Kind == BandItemKind.Memory).Layout);
    }

    [Fact]
    public void LeavesReadoutsThatAreNotPercentagesAlone()
    {
        var settings = new BandItemsSettings
        {
            PercentStyles =
            [
                new BandItemStyle { Kind = BandItemKind.Network, Style = BandPercentStyle.VerticalLabelGraph }
            ]
        };

        Assert.Empty(settings.Normalized().PercentStyles);
        Assert.Equal(
            BandItemLayout.RateRows,
            settings.Apply(MetricsSource.Items(MetricsSnapshot.Empty))
                .Single(item => item.Kind == BandItemKind.Network)
                .Layout);
    }

    [Fact]
    public void DropsStylesThatChangeNothingOrMakeNoSense()
    {
        var settings = new BandItemsSettings
        {
            PercentStyles =
            [
                new BandItemStyle { Kind = BandItemKind.Cpu, Style = BandPercentStyle.LabelAndValue },
                new BandItemStyle { Kind = BandItemKind.Memory, Style = (BandPercentStyle)99 },
                new BandItemStyle { Kind = BandItemKind.Gpu, Style = BandPercentStyle.VerticalLabelGraph },
                new BandItemStyle { Kind = BandItemKind.Gpu, Style = BandPercentStyle.VerticalLabelValueGraph }
            ]
        };

        var kept = settings.Normalized().PercentStyles;

        Assert.Equal(BandItemKind.Gpu, Assert.Single(kept).Kind);
        Assert.Equal(BandPercentStyle.VerticalLabelGraph, kept[0].Style);
    }

    [Fact]
    public void KeepsTheReadingWhenOnlyTheLayoutChanges()
    {
        var settings = new BandItemsSettings
        {
            PercentStyles =
            [
                new BandItemStyle { Kind = BandItemKind.Cpu, Style = BandPercentStyle.VerticalLabelValueGraph }
            ]
        };
        var trails = new MetricTrails();
        trails.Observe(new MetricsSnapshot(77, 0, 0, 0, 0, 0));

        var cpu = settings
            .Apply(MetricsSource.Items(new MetricsSnapshot(77, 0, 0, 0, 0, 0), trails))
            .Single(item => item.Kind == BandItemKind.Cpu);

        Assert.Equal("CPU", cpu.Label);
        Assert.Equal("77%", cpu.Values[0].Text);
        Assert.Equal([77d], cpu.Trail);
    }
}
