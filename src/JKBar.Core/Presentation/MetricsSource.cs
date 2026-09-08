// Turns a metrics snapshot into band readouts.
using JKBar.Core.Metrics;

namespace JKBar.Core.Presentation;

public static class MetricsSource
{
    /// <summary>In display order, left to right.</summary>
    /// <param name="trails">Recent load for the percentage readouts. Null when no graph style is in use.</param>
    public static IReadOnlyList<BandItem> Items(MetricsSnapshot snapshot, MetricTrails? trails = null)
    {
        var items = new List<BandItem>
        {
            Percent("CPU", snapshot.CpuPercent, trails)
        };

        if (snapshot.GpuAvailable)
        {
            items.Add(Percent("GPU", snapshot.GpuPercent, trails));
        }

        items.Add(Percent("RAM", snapshot.MemoryPercent, trails));
        items.Add(new BandItem(
            string.Empty,
            [Rate(snapshot.DiskReadBytesPerSecond), Rate(snapshot.DiskWriteBytesPerSecond)],
            Layout: BandItemLayout.IndicatorRows,
            Kind: BandItemKind.Disk));
        items.Add(new BandItem(
            "NET",
            [Rate(snapshot.NetworkOutBytesPerSecond), Rate(snapshot.NetworkInBytesPerSecond)],
            Layout: BandItemLayout.RateRows,
            Kind: BandItemKind.Network));

        return items;
    }

    /// <summary>What the readouts would say, so an unchanged band is not repainted at full screen width.</summary>
    public static string Signature(MetricsSnapshot snapshot) =>
        string.Join('|', Items(snapshot).SelectMany(item => item.Values).Select(value => value.Text));

    private static BandItem Percent(string label, double value, MetricTrails? trails)
    {
        var kind = label switch
        {
            "CPU" => BandItemKind.Cpu,
            "GPU" => BandItemKind.Gpu,
            _ => BandItemKind.Memory
        };

        return new BandItem(
            label,
            [new BandValue(ByteRate.Percent(value), ByteRate.WidestPercent)],
            Layout: BandItemLayout.StackedPercent,
            Kind: kind,
            Trail: trails?.For(kind));
    }

    private static BandValue Rate(double bytesPerSecond) =>
        new(ByteRate.PerSecond(bytesPerSecond), ByteRate.WidestRate);
}
