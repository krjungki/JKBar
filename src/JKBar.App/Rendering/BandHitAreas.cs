// What the band painted that the user can click, handed back so the window can hit-test without measuring again.
using JKBar.Core.Presentation;

namespace JKBar.App.Rendering;

/// <param name="NewsCramped">The slot is too narrow to hold a readable headline beside the name and the quote.</param>
internal readonly record struct BandHitAreas(
    Rectangle Image,
    Rectangle News,
    IReadOnlyList<ProcessIcon> ProcessIcons,
    IReadOnlyList<StatusIconArea> StatusIcons,
    bool NewsCramped);

/// <summary>Where a sync provider's icon was drawn, so hovering it can explain the badge.</summary>
internal readonly record struct StatusIconArea(BandItemKind Kind, Rectangle Bounds)
{
    internal static StatusIconArea? At(IReadOnlyList<StatusIconArea> areas, Point point)
    {
        foreach (var area in areas)
        {
            if (area.Bounds.Contains(point))
            {
                return area;
            }
        }

        return null;
    }
}
