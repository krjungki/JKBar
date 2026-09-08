// Recent load for the readouts that can be drawn as a graph.
using JKBar.Core.Metrics;

namespace JKBar.Core.Presentation;

public sealed class MetricTrails
{
    private readonly MetricTrail _cpu = new();
    private readonly MetricTrail _gpu = new();
    private readonly MetricTrail _memory = new();

    /// <summary>Recorded on every sample regardless of the chosen style, so switching to a graph is not blank.</summary>
    public void Observe(MetricsSnapshot snapshot)
    {
        _cpu.Add(snapshot.CpuPercent);
        _memory.Add(snapshot.MemoryPercent);

        if (snapshot.GpuAvailable)
        {
            _gpu.Add(snapshot.GpuPercent);
        }
    }

    public IReadOnlyList<double> For(BandItemKind kind) => kind switch
    {
        BandItemKind.Cpu => _cpu.Readings(),
        BandItemKind.Gpu => _gpu.Readings(),
        BandItemKind.Memory => _memory.Readings(),
        _ => []
    };
}
