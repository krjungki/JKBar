// Turns a metrics snapshot into band readouts.
using System.Drawing;
using JKBar.Core.Metrics;

namespace JKBar.Core.Presentation;

public static class MetricsSource
{
    // Mid-saturation so they stay legible whether the band ends up light or dark.
    private static readonly Color Cpu = Color.FromArgb(47, 128, 237);
    private static readonly Color Memory = Color.FromArgb(226, 131, 59);
    private static readonly Color Disk = Color.FromArgb(155, 111, 208);
    private static readonly Color Network = Color.FromArgb(47, 163, 124);

    /// <summary>In display order, left to right.</summary>
    public static IReadOnlyList<BandItem> Items(MetricsSnapshot snapshot) =>
    [
        new BandItem("CPU", ByteRate.Percent(snapshot.CpuPercent), Cpu),
        new BandItem("RAM", ByteRate.Percent(snapshot.MemoryPercent), Memory),
        new BandItem("DISK", Pair(snapshot.DiskReadBytesPerSecond, snapshot.DiskWriteBytesPerSecond), Disk),
        new BandItem("NET", Pair(snapshot.NetworkInBytesPerSecond, snapshot.NetworkOutBytesPerSecond), Network)
    ];

    /// <summary>What the readouts would say, so an unchanged band is not repainted at full screen width.</summary>
    public static string Signature(MetricsSnapshot snapshot) =>
        string.Join('|', Items(snapshot).Select(item => item.Value));

    private static string Pair(double down, double up) =>
        "\u2193" + ByteRate.PerSecond(down) + "  \u2191" + ByteRate.PerSecond(up);
}
