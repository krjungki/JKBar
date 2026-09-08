// Raises a warning only after a reading stays high, so a one second spike stays silent.
using JKBar.Core.Metrics;

namespace JKBar.Core.Alerts;

public sealed class SystemThresholdWatcher(int percentThreshold = 90, int sustainedSamples = 5)
{
    private int _cpuStreak;
    private int _memoryStreak;

    public IReadOnlyList<NotchAlert> Observe(MetricsSnapshot snapshot)
    {
        var alerts = new List<NotchAlert>(2);

        if (Sustained(snapshot.CpuPercent, ref _cpuStreak))
        {
            alerts.Add(new NotchAlert(
                AlertCategory.System,
                "system.cpu",
                "CPU 사용량 높음",
                $"{snapshot.CpuPercent:0}% 지속",
                AlertSeverity.Warning));
        }

        if (Sustained(snapshot.MemoryPercent, ref _memoryStreak))
        {
            alerts.Add(new NotchAlert(
                AlertCategory.System,
                "system.memory",
                "메모리 사용량 높음",
                $"{snapshot.MemoryPercent:0}% 지속",
                AlertSeverity.Warning));
        }

        return alerts;
    }

    private bool Sustained(double percent, ref int streak)
    {
        if (!double.IsFinite(percent) || percent < percentThreshold)
        {
            streak = 0;
            return false;
        }

        streak++;
        return streak == sustainedSamples;
    }
}
