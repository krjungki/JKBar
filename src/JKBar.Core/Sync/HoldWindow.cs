// Ported from JKMon (packages/JKMon/src/JKMon.Core/Sync). Keep behaviour changes in sync with the original.
namespace JKBar.Core.Sync;

/// <summary>
/// Keeps a signal asserted for a while after the last activity. Short bursts finish between polls, so without a
/// hold window they would never be visible on screen.
/// </summary>
public sealed class HoldWindow
{
    private readonly TimeSpan _hold;
    private DateTimeOffset _lastMarked = DateTimeOffset.MinValue;

    public HoldWindow(TimeSpan hold) => _hold = hold;

    public void Mark(DateTimeOffset now) => _lastMarked = now;

    public bool IsActive(DateTimeOffset now) =>
        _lastMarked != DateTimeOffset.MinValue && now - _lastMarked <= _hold;
}
