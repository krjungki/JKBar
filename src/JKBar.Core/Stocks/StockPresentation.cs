// How one quote reads on the band.
namespace JKBar.Core.Stocks;

public static class StockPresentation
{
    public static string Marker(StockDirection direction) => direction switch
    {
        StockDirection.Rising => "▲",
        StockDirection.Falling => "▼",
        _ => "-"
    };

    /// <summary>One line per symbol, the way a headline is one line per article.</summary>
    public static string Line(StockQuote quote)
    {
        var change = quote.ChangePercent.Trim();

        return change.Length == 0
            ? $"{quote.Name} {quote.Price}"
            : $"{quote.Name} {quote.Price} {Marker(quote.Direction)}{change}%";
    }
}
