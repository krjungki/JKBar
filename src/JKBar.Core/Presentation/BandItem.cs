// One readout in the band's right slot. Sources produce these; the renderer knows nothing about where they came from.
using System.Drawing;

namespace JKBar.Core.Presentation;

/// <param name="Template">
/// The widest text this value will ever take. Layout reserves that width instead of measuring the current text,
/// so a reading growing from "9%" to "100%" cannot shove everything beside it sideways.
/// </param>
public sealed record BandValue(string Text, string Template);

public enum BandItemLayout
{
    Inline,
    StackedPercent,
    RateRows,
    IndicatorRows,
    StatusIcon,
    VerticalLabelGraph,
    VerticalLabelValueGraph
}

/// <summary>How a percentage readout is drawn. The numbers are stored in settings, so they must not be reordered.</summary>
public enum BandPercentStyle
{
    LabelAndValue = 0,
    VerticalLabelGraph = 1,
    VerticalLabelValueGraph = 2
}

public enum BandItemKind
{
    Custom,
    Cpu,
    Gpu,
    Memory,
    Disk,
    Network,
    Clock,
    GlobalSecureAccess,
    OneDrive,
    Syncthing
}

public enum BandItemBadge
{
    None,
    Attention,
    Good
}

/// <param name="Accent">Null leaves the colour to the renderer, which picks for contrast against the band.</param>
/// <param name="Badge">The small mark the renderer stamps on the item's corner.</param>
/// <param name="Trail">Recent readings, oldest first, for the layouts that draw a graph. Null means none are kept.</param>
public sealed record BandItem(
    string Label,
    IReadOnlyList<BandValue> Values,
    Color? Accent = null,
    string? LabelTemplate = null,
    BandItemLayout Layout = BandItemLayout.Inline,
    BandItemKind Kind = BandItemKind.Custom,
    BandItemBadge Badge = BandItemBadge.None,
    IReadOnlyList<double>? Trail = null)
{
    public BandItem(string label, string value, string template, Color? accent = null)
        : this(label, [new BandValue(value, template)], accent)
    {
    }

    /// <summary>Constant labels size themselves; only ones that change, like a date, need a wider yardstick.</summary>
    public string LabelYardstick => LabelTemplate ?? Label;
}
