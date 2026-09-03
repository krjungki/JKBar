// One readout in the band's right slot. Sources produce these; the renderer knows nothing about where they came from.
using System.Drawing;

namespace JKBar.Core.Presentation;

/// <param name="Accent">Null leaves the colour to the renderer, which picks for contrast against the band.</param>
public sealed record BandItem(string Label, string Value, Color? Accent = null);
