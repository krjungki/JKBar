// Symbols the user registered for the band to cycle through.
using JKBar.Core.Stocks;

namespace JKBar.Core.Settings;

/// <param name="Code">Naver's own symbol, so a renamed listing keeps quoting.</param>
public sealed record WatchedStock
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsIndex { get; init; }

    public WatchedStock Normalized() => this with
    {
        Code = (Code ?? string.Empty).Trim(),
        Name = (Name ?? string.Empty).Trim()
    };

    public static WatchedStock From(StockMatch match) =>
        new WatchedStock { Code = match.Code, Name = match.Name, IsIndex = match.IsIndex }.Normalized();
}

public sealed record StockWatchSettings
{
    /// <summary>The band cycles through these one at a time, so a long list only makes each wait longer.</summary>
    public const int MaximumItems = 10;

    public bool Enabled { get; init; }
    public WatchedStock[] Items { get; init; } = [];
    public int RefreshSeconds { get; init; } = 30;
    public int RotationSeconds { get; init; } = 6;

    public StockWatchSettings Normalized() => this with
    {
        Items = (Items ?? [])
            .Select(item => (item ?? new WatchedStock()).Normalized())
            .Where(item => item.Code.Length > 0)
            .DistinctBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .Take(MaximumItems)
            .ToArray(),
        RefreshSeconds = Math.Clamp(RefreshSeconds, 10, 600),
        RotationSeconds = Math.Clamp(RotationSeconds, 3, 60)
    };

    /// <summary>Nothing is fetched for an empty list, so an unused feature costs no requests.</summary>
    public bool IsActive => Enabled && Normalized().Items.Length > 0;
}
