// What the band painted that the user can click, handed back so the window can hit-test without measuring again.
using JKBar.Core.Presentation;

namespace JKBar.App.Rendering;

internal readonly record struct BandHitAreas(Rectangle News, IReadOnlyList<ProcessIcon> ProcessIcons);
