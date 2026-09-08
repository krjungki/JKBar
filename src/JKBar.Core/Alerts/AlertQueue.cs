// Decides which alert the notch shows and for how long.
namespace JKBar.Core.Alerts;

public sealed class AlertQueue(AlertPolicy policy)
{
    private readonly List<NotchAlert> _pending = [];
    private readonly Dictionary<string, DateTimeOffset> _lastAccepted = new(StringComparer.Ordinal);

    private DateTimeOffset _expiresAt;

    public NotchAlert? Current { get; private set; }

    /// <param name="quiet">Drops anything below a warning, for full-screen apps and presentations.</param>
    public bool Submit(NotchAlert alert, DateTimeOffset now, bool quiet = false)
    {
        if (quiet && alert.Severity != AlertSeverity.Warning)
        {
            return false;
        }

        if (Current?.Key == alert.Key)
        {
            _lastAccepted[alert.Key] = now;
            Show(alert, now);
            return true;
        }

        var queued = _pending.FindIndex(entry => entry.Key == alert.Key);
        if (queued >= 0)
        {
            _lastAccepted[alert.Key] = now;
            _pending[queued] = alert;
            return true;
        }

        var cooldown = alert.Cooldown ?? policy.RepeatCooldown;
        if (_lastAccepted.TryGetValue(alert.Key, out var last) && now - last < cooldown)
        {
            return false;
        }

        _lastAccepted[alert.Key] = now;

        if (Current is null)
        {
            Show(alert, now);
        }
        else
        {
            _pending.Add(alert);
        }

        return true;
    }

    /// <summary>Retires an expired alert and promotes the next one. Returns what should be on screen now.</summary>
    public NotchAlert? Tick(DateTimeOffset now)
    {
        if (Current is not null && now < _expiresAt)
        {
            return Current;
        }

        Current = null;
        if (_pending.Count == 0)
        {
            return null;
        }

        var next = _pending
            .OrderByDescending(alert => alert.Severity)
            .First();
        _pending.Remove(next);
        Show(next, now);

        return Current;
    }

    private void Show(NotchAlert alert, DateTimeOffset now)
    {
        Current = alert;
        _expiresAt = now + policy.DwellFor(alert.Severity);
    }
}
