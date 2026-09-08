// What the band shows for one registered symbol, and what the settings window offers after a search.
namespace JKBar.Core.Stocks;

public enum StockDirection
{
    Flat,
    Rising,
    Falling
}

/// <param name="Price">Already grouped by Naver, so the band shows the same digits the site does.</param>
/// <param name="ChangePercent">Unsigned; the direction says which way it went.</param>
public sealed record StockQuote(
    string Code,
    string Name,
    string Price,
    string ChangePercent,
    StockDirection Direction);

/// <param name="IsIndex">Indexes and shares are quoted through different paths, so the kind has to be kept.</param>
public sealed record StockMatch(string Code, string Name, bool IsIndex, string Kind);
