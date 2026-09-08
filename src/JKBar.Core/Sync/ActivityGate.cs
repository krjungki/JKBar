// Ported from JKMon (packages/JKMon/src/JKMon.Core/Sync). Keep behaviour changes in sync with the original.
namespace JKBar.Core.Sync;

/// <summary>
/// Turns a monotonic byte counter into an on/off activity signal.
///
/// The counter measures file I/O, not network transfer, so a client reading its own database or cache looks
/// identical to one reading a file in order to upload it. A 10 minute idle measurement of OneDrive sat under
/// 8 KiB/s for 198 of 200 samples and then produced a single two-sample burst peaking at 988 KiB/s, which no
/// threshold separates from a real transfer. Requiring the rate to stay up for several consecutive samples is
/// what rejects that burst, so the gate asks for sustained activity before it asserts and then holds the signal
/// across the short lulls between the bursts of a real transfer.
/// </summary>
public sealed class ActivityGate
{
    private readonly long _thresholdBytesPerSecond;
    private readonly TimeSpan _hold;
    private readonly int _samplesToAssert;

    private long _lastTotal = -1;
    private int _consecutive;
    private DateTimeOffset _lastSampled;
    private DateTimeOffset _lastActive = DateTimeOffset.MinValue;

    public ActivityGate(long thresholdBytesPerSecond, TimeSpan hold, int samplesToAssert = 1)
    {
        _thresholdBytesPerSecond = thresholdBytesPerSecond;
        _hold = hold;
        _samplesToAssert = Math.Max(1, samplesToAssert);
    }

    public double LastRateBytesPerSecond { get; private set; }

    /// <summary>Consecutive samples over the threshold, exposed so a diagnostic log can explain a decision.</summary>
    public int ConsecutiveSamples => _consecutive;

    public bool Update(long totalBytes, DateTimeOffset now)
    {
        if (_lastTotal < 0 || now <= _lastSampled)
        {
            _lastTotal = totalBytes;
            _lastSampled = now;
            return IsWithinHold(now);
        }

        var elapsed = (now - _lastSampled).TotalSeconds;
        var delta = totalBytes >= _lastTotal ? totalBytes - _lastTotal : 0;

        _lastTotal = totalBytes;
        _lastSampled = now;
        LastRateBytesPerSecond = delta / elapsed;

        if (LastRateBytesPerSecond < _thresholdBytesPerSecond)
        {
            _consecutive = 0;
            return IsWithinHold(now);
        }

        _consecutive++;

        // An already asserted transfer keeps its signal through the hold, so a lull does not restart the count.
        if (_consecutive < _samplesToAssert && !IsWithinHold(now))
        {
            return false;
        }

        _lastActive = now;
        return true;
    }

    private bool IsWithinHold(DateTimeOffset now) =>
        _lastActive != DateTimeOffset.MinValue && now - _lastActive <= _hold;
}
