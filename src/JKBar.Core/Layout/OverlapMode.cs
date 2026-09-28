// How the bar coexists with other windows: it either displaces them or hides behind them.
namespace JKBar.Core.Layout;

/// <summary>Values are persisted as numbers; 0 was the removed floating mode and now normalizes to reserving.</summary>
public enum OverlapMode
{
    /// <summary>
    /// Reserves a band so maximised windows stop below it. Windows can only reserve a whole screen edge, never a
    /// centre strip, so the full width is taken and the desktop shows either side of the bar.
    /// </summary>
    ReserveTopEdge = 1,

    /// <summary>Sits at the bottom of the z-order, so it appears only when the desktop does.</summary>
    PinnedToDesktop = 2
}
