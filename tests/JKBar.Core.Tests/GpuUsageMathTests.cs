// Verifies that process-level GPU counters are combined per physical engine rather than blindly summed.
using JKBar.Core.Metrics;

namespace JKBar.Core.Tests;

public class GpuUsageMathTests
{
    [Fact]
    public void SumsProcessesOnTheSamePhysicalEngine()
    {
        KeyValuePair<string, double>[] samples =
        [
            new("pid_10_luid_0x1_phys_0_eng_0_engtype_3D", 20),
            new("pid_20_luid_0x1_phys_0_eng_0_engtype_3D", 15)
        ];

        Assert.Equal(35, GpuUsageMath.OverallPercent(samples));
    }

    [Fact]
    public void UsesTheBusiestEngineInsteadOfSummingIndependentEngines()
    {
        KeyValuePair<string, double>[] samples =
        [
            new("pid_10_luid_0x1_phys_0_eng_0_engtype_3D", 40),
            new("pid_10_luid_0x1_phys_0_eng_1_engtype_Copy", 25)
        ];

        Assert.Equal(40, GpuUsageMath.OverallPercent(samples));
    }

    [Fact]
    public void ClampsBrokenOrOverlappingSamples()
    {
        KeyValuePair<string, double>[] samples =
        [
            new("pid_10_luid_0x1_phys_0_eng_0_engtype_3D", 80),
            new("pid_20_luid_0x1_phys_0_eng_0_engtype_3D", 60),
            new("invalid", double.NaN)
        ];

        Assert.Equal(100, GpuUsageMath.OverallPercent(samples));
    }
}