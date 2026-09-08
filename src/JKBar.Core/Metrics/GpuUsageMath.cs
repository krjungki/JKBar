// Aggregates process-level GPU Engine counters into the busiest physical engine percentage.
namespace JKBar.Core.Metrics;

public static class GpuUsageMath
{
    public static double OverallPercent(IEnumerable<KeyValuePair<string, double>> samples)
    {
        var engines = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var (instance, percent) in samples)
        {
            if (!double.IsFinite(percent) || percent <= 0)
            {
                continue;
            }

            var key = PhysicalEngineKey(instance);
            engines[key] = engines.GetValueOrDefault(key) + percent;
        }

        return engines.Count == 0 ? 0 : Math.Clamp(engines.Values.Max(), 0, 100);
    }

    private static string PhysicalEngineKey(string instance)
    {
        var luid = instance.IndexOf("luid_", StringComparison.OrdinalIgnoreCase);
        return luid >= 0 ? instance[luid..] : instance;
    }
}