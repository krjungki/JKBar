// The recent readings of one percentage, so the band can show the shape of the load and not only its latest value.
namespace JKBar.Core.Metrics;

public sealed class MetricTrail(int capacity = MetricTrail.DefaultCapacity)
{
    /// <summary>Enough readings to fill a strip a band's height wide without holding minutes of stale history.</summary>
    public const int DefaultCapacity = 40;

    private readonly double[] _readings = new double[Math.Clamp(capacity, 2, 512)];
    private int _count;
    private int _next;

    public int Capacity => _readings.Length;

    public void Add(double percent)
    {
        _readings[_next] = double.IsFinite(percent) ? Math.Clamp(percent, 0, 100) : 0;
        _next = (_next + 1) % _readings.Length;
        _count = Math.Min(_count + 1, _readings.Length);
    }

    /// <summary>Oldest reading first, so a caller can plot it left to right.</summary>
    public IReadOnlyList<double> Readings()
    {
        var readings = new double[_count];
        var start = (_next - _count + _readings.Length) % _readings.Length;
        for (var i = 0; i < _count; i++)
        {
            readings[i] = _readings[(start + i) % _readings.Length];
        }

        return readings;
    }
}
