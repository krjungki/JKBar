// Ported from JKMon (packages/JKMon/src/JKMon.Core/Update/UpdateSchedule.cs). Keep behaviour changes in sync.
namespace JKBar.Core.Update;

public enum UpdateCheckFrequency
{
    Never = 0,
    Daily = 1,
    Weekly = 2
}

public static class UpdateSchedule
{
    public static TimeSpan IntervalOf(UpdateCheckFrequency frequency) => frequency switch
    {
        UpdateCheckFrequency.Daily => TimeSpan.FromDays(1),
        UpdateCheckFrequency.Weekly => TimeSpan.FromDays(7),
        _ => TimeSpan.Zero
    };

    /// <param name="alreadyCheckedThisRun">Keeps a startup check from repeating every time the timer ticks.</param>
    public static bool IsDue(
        UpdateCheckFrequency frequency,
        DateTimeOffset lastCheckUtc,
        DateTimeOffset appStartedUtc,
        DateTimeOffset nowUtc,
        bool checkAtStartup,
        bool alreadyCheckedThisRun)
    {
        if (frequency == UpdateCheckFrequency.Never)
        {
            return false;
        }

        if (checkAtStartup && !alreadyCheckedThisRun)
        {
            return true;
        }

        var since = checkAtStartup ? lastCheckUtc : Later(lastCheckUtc, appStartedUtc);
        if (since == default)
        {
            return true;
        }

        // A clock that moved backwards would otherwise postpone checks indefinitely.
        return nowUtc < since || nowUtc - since >= IntervalOf(frequency);
    }

    private static DateTimeOffset Later(DateTimeOffset first, DateTimeOffset second) =>
        first > second ? first : second;
}
