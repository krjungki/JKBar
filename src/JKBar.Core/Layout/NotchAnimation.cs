// The curve the bar grows and shrinks along, kept in Core so it can be tested without putting a window on screen.
namespace JKBar.Core.Layout;

public static class NotchAnimation
{
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(260);

    /// <summary>Decelerating: quick to leave, slow to settle. A linear ramp reads as mechanical at this size.</summary>
    public static double EaseOutCubic(double progress)
    {
        var remaining = 1d - Math.Clamp(progress, 0d, 1d);
        return 1d - (remaining * remaining * remaining);
    }

    public static NotchMetrics Between(NotchMetrics from, NotchMetrics to, double progress)
    {
        var eased = EaseOutCubic(progress);

        return new NotchMetrics(
            Step(from.Width, to.Width, eased),
            Step(from.Height, to.Height, eased),
            Step(from.BottomCornerRadius, to.BottomCornerRadius, eased));
    }

    private static int Step(int from, int to, double eased) => (int)Math.Round(from + ((to - from) * eased));
}
