// Pins the display rules: one alert at a time, warnings first, repeats collapsed.
using JKBar.Core.Alerts;

namespace JKBar.Core.Tests;

public class AlertQueueTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private static NotchAlert Alert(string key, AlertSeverity severity = AlertSeverity.Change) =>
        new(AlertCategory.System, key, key, Severity: severity);

    [Fact]
    public void ShowsTheFirstAlertImmediately()
    {
        var queue = new AlertQueue(AlertPolicy.Default);

        Assert.True(queue.Submit(Alert("a"), Start));
        Assert.Equal("a", queue.Current?.Key);
    }

    [Fact]
    public void KeepsTheCurrentAlertUntilItsDwellEnds()
    {
        var queue = new AlertQueue(AlertPolicy.Default);
        queue.Submit(Alert("a"), Start);

        Assert.Equal("a", queue.Tick(Start.AddSeconds(1))?.Key);
        Assert.Null(queue.Tick(Start.AddSeconds(3)));
    }

    [Fact]
    public void GivesWarningsALongerDwellThanChanges()
    {
        var queue = new AlertQueue(AlertPolicy.Default);
        queue.Submit(Alert("warn", AlertSeverity.Warning), Start);

        Assert.Equal("warn", queue.Tick(Start.AddSeconds(3))?.Key);
        Assert.Null(queue.Tick(Start.AddSeconds(5)));
    }

    [Fact]
    public void PromotesTheMostSevereQueuedAlertFirst()
    {
        var queue = new AlertQueue(AlertPolicy.Default);
        queue.Submit(Alert("current"), Start);
        queue.Submit(Alert("later"), Start);
        queue.Submit(Alert("urgent", AlertSeverity.Warning), Start);

        Assert.Equal("urgent", queue.Tick(Start.AddSeconds(3))?.Key);
    }

    [Fact]
    public void DropsARepeatOfTheSameKeyWithinTheCooldown()
    {
        var queue = new AlertQueue(AlertPolicy.Default);
        queue.Submit(Alert("a"), Start);
        queue.Tick(Start.AddSeconds(3));

        Assert.False(queue.Submit(Alert("a"), Start.AddSeconds(4)));
    }

    [Fact]
    public void AcceptsTheSameKeyAgainAfterTheCooldown()
    {
        var queue = new AlertQueue(AlertPolicy.Default);
        queue.Submit(Alert("a"), Start);
        queue.Tick(Start.AddSeconds(3));

        Assert.True(queue.Submit(Alert("a"), Start.AddSeconds(31)));
    }

    [Fact]
    public void QuietModeLetsOnlyWarningsThrough()
    {
        var queue = new AlertQueue(AlertPolicy.Default);

        Assert.False(queue.Submit(Alert("a"), Start, quiet: true));
        Assert.True(queue.Submit(Alert("warn", AlertSeverity.Warning), Start, quiet: true));
    }

    [Fact]
    public void RefreshesTheShownAlertWhenTheSameKeyArrivesAgain()
    {
        var queue = new AlertQueue(AlertPolicy.Default);
        queue.Submit(new NotchAlert(AlertCategory.Audio, "volume", "40%"), Start);

        Assert.True(queue.Submit(new NotchAlert(AlertCategory.Audio, "volume", "55%"), Start.AddSeconds(1)));
        Assert.Equal("55%", queue.Tick(Start.AddSeconds(2.5))?.Title);
        Assert.Null(queue.Tick(Start.AddSeconds(3.5)));
    }

    [Fact]
    public void ReplacesAQueuedAlertWithTheNewerValue()
    {
        var queue = new AlertQueue(AlertPolicy.Default);
        queue.Submit(Alert("shown"), Start);
        queue.Submit(new NotchAlert(AlertCategory.Audio, "volume", "40%"), Start);
        queue.Submit(new NotchAlert(AlertCategory.Audio, "volume", "55%"), Start);

        Assert.Equal("55%", queue.Tick(Start.AddSeconds(3))?.Title);
        Assert.Null(queue.Tick(Start.AddSeconds(6)));
    }

    [Fact]
    public void LetsAnAlertShortenItsOwnRepeatInterval()
    {
        var queue = new AlertQueue(AlertPolicy.Default);
        var burst = new NotchAlert(AlertCategory.Audio, "volume", "40%", Cooldown: TimeSpan.Zero);
        queue.Submit(burst, Start);
        queue.Tick(Start.AddSeconds(3));

        Assert.True(queue.Submit(burst, Start.AddSeconds(4)));
    }
}
