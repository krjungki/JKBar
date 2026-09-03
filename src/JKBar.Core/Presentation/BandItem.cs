// One readout in the band's right slot. Sources produce these; the renderer knows nothing about where they came from.
using System.Drawing;

namespace JKBar.Core.Presentation;

/// <param name="Template">
/// The widest text this value will ever take. Layout reserves that width instead of measuring the current text,
/// so a reading growing from "9%" to "100%" cannot shove everything beside it sideways.
/// </param>
public sealed record BandValue(string Text, string Template);

/// <param name="Accent">Null leaves the colour to the renderer, which picks for contrast against the band.</param>
public sealed record BandItem(
    string Label,
    IReadOnlyList<BandValue> Values,
    Color? Accent = null,
    string? LabelTemplate = null)
{
    public BandItem(string label, string value, string template, Color? accent = null)
        : this(label, [new BandValue(value, template)], accent)
    {
    }

    /// <summary>Constant labels size themselves; only ones that change, like a date, need a wider yardstick.</summary>
    public string LabelYardstick => LabelTemplate ?? Label;
}
