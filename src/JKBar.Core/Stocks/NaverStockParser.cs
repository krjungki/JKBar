// Reads Naver's two finance responses. Kept free of HTTP so the shapes can be checked without the network.
using System.Text.Json;

namespace JKBar.Core.Stocks;

public static class NaverStockParser
{
    /// <summary>What the search box offers. An unknown name simply comes back with no items.</summary>
    public static IReadOnlyList<StockMatch> Matches(string json)
    {
        if (Root(json) is not { } root || !root.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var matches = new List<StockMatch>();
        foreach (var item in items.EnumerateArray())
        {
            var code = Text(item, "code");
            var name = Text(item, "name");
            if (code.Length == 0 || name.Length == 0)
            {
                continue;
            }

            var category = Text(item, "category");
            matches.Add(new StockMatch(
                code,
                name,
                !string.Equals(category, "stock", StringComparison.OrdinalIgnoreCase),
                Text(item, "typeName")));
        }

        return matches;
    }

    /// <param name="names">What the user registered, which is kept over Naver's own spacing of the same name.</param>
    public static IReadOnlyList<StockQuote> Quotes(string json, IReadOnlyDictionary<string, string>? names = null)
    {
        if (Root(json) is not { } root || !root.TryGetProperty("datas", out var datas)
            || datas.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var quotes = new List<StockQuote>();
        foreach (var item in datas.EnumerateArray())
        {
            var code = Text(item, "itemCode");
            var price = Text(item, "closePrice");
            if (code.Length == 0 || price.Length == 0)
            {
                continue;
            }

            var name = names is not null && names.TryGetValue(code, out var registered) && registered.Length > 0
                ? registered
                : Text(item, "stockName");

            quotes.Add(new StockQuote(
                code,
                name.Length == 0 ? code : name,
                price,
                // Naver signs a fall, and the band already draws an arrow, so the sign is dropped here.
                Text(item, "fluctuationsRatio").TrimStart('+', '-'),
                Direction(item)));
        }

        return quotes;
    }

    private static StockDirection Direction(JsonElement item)
    {
        if (!item.TryGetProperty("compareToPreviousPrice", out var compare)
            || compare.ValueKind != JsonValueKind.Object)
        {
            return StockDirection.Flat;
        }

        return Text(compare, "name").ToUpperInvariant() switch
        {
            "RISING" or "UPPER_LIMIT" => StockDirection.Rising,
            "FALLING" or "LOWER_LIMIT" => StockDirection.Falling,
            _ => StockDirection.Flat
        };
    }

    private static JsonElement? Root(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
