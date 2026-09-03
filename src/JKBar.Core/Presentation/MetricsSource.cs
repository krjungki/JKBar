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
        new BandItem("CPU", ByteRate.Percent(snapshot.CpuPercent), ByteRate.WidestPercent, Cpu),
        new BandItem("RAM", ByteRate.Percent(snapshot.MemoryPercent), ByteRate.WidestPercent, Memory),
        new BandItem(
            "DISK",
            [Rate(Down, snapshot.DiskReadBytesPerSecond), Rate(Up, snapshot.DiskWriteBytesPerSecond)],
            Disk),
        new BandItem(
            "NET",
            [Rate(Down, snapshot.NetworkInBytesPerSecond), Rate(Up, snapshot.NetworkOutBytesPerSecond)],
            Network)
    ];

    /// <summary>What the readouts would say, so an unchanged band is not repainted at full screen width.</summary>
    public static string Signature(MetricsSnapshot snapshot) =>
        string.Join('|', Items(snapshot).SelectMany(item => item.Values).Select(value => value.Text));

    private const string Down = "\u2193";
    private const string Up = "\u2191";

    private static BandValue Rate(string arrow, double bytesPerSecond) =>
        new(arrow + ByteRate.PerSecond(bytesPerSecond), arrow + ByteRate.WidestRate);
}
