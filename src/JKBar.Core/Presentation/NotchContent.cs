// What the notch shows: one line at rest, two when an alert is up.
namespace JKBar.Core.Presentation;

public sealed record NotchContent(string Primary, string? Secondary = null);
