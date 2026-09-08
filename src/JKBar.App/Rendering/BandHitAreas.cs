// What the band painted that the user can click, handed back so the window can hit-test without measuring again.
using JKBar.Core.Presentation;

namespace JKBar.App.Rendering;

/// <param name="NewsCramped">The slot is too narrow to hold a readable headline beside the name and the quote.</param>
internal readonly record struct BandHitAreas(
    Rectangle News,
    IReadOnlyList<ProcessIcon> ProcessIcons,
    bool NewsCramped);
