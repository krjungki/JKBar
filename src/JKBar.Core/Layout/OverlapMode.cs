// How the bar coexists with other windows. The three answers differ in whether it covers, displaces, or hides.
namespace JKBar.Core.Layout;

public enum OverlapMode
{
    /// <summary>Floats above everything and covers whatever is underneath.</summary>
    Floating,

    /// <summary>
    /// Reserves a band so maximised windows stop below it. Windows can only reserve a whole screen edge, never a
    /// centre strip, so the full width is taken and the desktop shows either side of the bar.
    /// </summary>
    ReserveTopEdge,

    /// <summary>Sits at the bottom of the z-order, so it appears only when the desktop does.</summary>
    PinnedToDesktop
}
