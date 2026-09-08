using JKBar.Core.Settings;
using JKBar.Core.Stocks;

namespace JKBar.Core.Tests;

public sealed class NaverStockParserTests
{
    private const string SearchReply = """
        {"query":"삼성전자","items":[
          {"code":"005930","name":"삼성전자","typeCode":"KOSPI","typeName":"코스피","category":"stock"},
          {"code":"0162Z0","name":"RISE 삼성전자SK하이닉스채권혼합50","typeName":"코스피","category":"stock"},
          {"code":"KOSPI","name":"코스피","typeCode":"INDEX","typeName":"국내지수","category":"index"},
          {"name":"코드가 없는 항목","category":"stock"}
        ]}
        """;

    private const string QuoteReply = """
        {"pollingInterval":7000,"datas":[
          {"itemCode":"005930","stockName":"삼성 전자","closePrice":"276,750",
           "compareToPreviousClosePrice":"6,750",
           "compareToPreviousPrice":{"code":"2","text":"상승","name":"RISING"},
           "fluctuationsRatio":"2.50","marketStatus":"OPEN"},
          {"itemCode":"000660","stockName":"SK하이닉스","closePrice":"412,000",
           "compareToPreviousPrice":{"code":"5","text":"하락","name":"FALLING"},
           "fluctuationsRatio":"1.20"},
          {"itemCode":"KOSPI","stockName":"코스피","closePrice":"7,133.41",
           "compareToPreviousPrice":{"code":"3","text":"보합","name":"STEADY"},
           "fluctuationsRatio":"0.00"}
        ]}
        """;

    [Fact]
    public void ReadsWhatTheSearchOffers()
    {
        var matches = NaverStockParser.Matches(SearchReply);

        Assert.Equal(3, matches.Count);
        Assert.Equal("005930", matches[0].Code);
        Assert.Equal("삼성전자", matches[0].Name);
        Assert.False(matches[0].IsIndex);
        Assert.Equal("코스피", matches[0].Kind);
    }

    [Fact]
    public void TellsAnIndexFromAShare()
    {
        var matches = NaverStockParser.Matches(SearchReply);

        Assert.True(matches.Single(match => match.Code == "KOSPI").IsIndex);
        Assert.False(matches.Single(match => match.Code == "005930").IsIndex);
    }

    [Theory]
    [InlineData("""{"query":"없는이름","items":[]}""")]
    [InlineData("")]
    [InlineData("<html>차단 안내</html>")]
    [InlineData("""{"unexpected":true}""")]
    public void ReturnsNothingRatherThanFailing(string reply)
    {
        Assert.Empty(NaverStockParser.Matches(reply));
        Assert.Empty(NaverStockParser.Quotes(reply));
    }

    [Fact]
    public void ReadsThePriceAndTheMove()
    {
        var quotes = NaverStockParser.Quotes(QuoteReply);

        Assert.Equal(3, quotes.Count);
        Assert.Equal("276,750", quotes[0].Price);
        Assert.Equal("2.50", quotes[0].ChangePercent);
        Assert.Equal(StockDirection.Rising, quotes[0].Direction);
        Assert.Equal(StockDirection.Falling, quotes[1].Direction);
        Assert.Equal(StockDirection.Flat, quotes[2].Direction);
    }

    [Fact]
    public void LeavesTheSignToTheArrow()
    {
        // Naver signs a fall, and the band draws an arrow beside it, so `▼-0.89%` would say it twice.
        var quotes = NaverStockParser.Quotes("""
            {"datas":[{"itemCode":"005380","stockName":"현대차","closePrice":"389,500",
             "compareToPreviousPrice":{"name":"FALLING"},"fluctuationsRatio":"-0.89"}]}
            """);

        Assert.Equal("0.89", quotes[0].ChangePercent);
        Assert.Equal("현대차 389,500 ▼0.89%", StockPresentation.Line(quotes[0]));
    }

    [Fact]
    public void KeepsTheNameTheUserRegistered()
    {
        var quotes = NaverStockParser.Quotes(
            QuoteReply,
            new Dictionary<string, string> { ["005930"] = "삼성전자" });

        Assert.Equal("삼성전자", quotes[0].Name);
        Assert.Equal("SK하이닉스", quotes[1].Name);
    }

    [Fact]
    public void SkipsAnEntryWithoutAPrice()
    {
        var quotes = NaverStockParser.Quotes("""{"datas":[{"itemCode":"005930","stockName":"삼성전자"}]}""");

        Assert.Empty(quotes);
    }

    [Theory]
    [InlineData(StockDirection.Rising, "삼성전자 276,750 ▲2.50%")]
    [InlineData(StockDirection.Falling, "삼성전자 276,750 ▼2.50%")]
    [InlineData(StockDirection.Flat, "삼성전자 276,750 -2.50%")]
    public void ReadsAsOneLine(StockDirection direction, string expected)
    {
        var quote = new StockQuote("005930", "삼성전자", "276,750", "2.50", direction);

        Assert.Equal(expected, StockPresentation.Line(quote));
    }

    [Fact]
    public void LeavesOutAMoveItDoesNotHave()
    {
        var quote = new StockQuote("KOSPI", "코스피", "7,133.41", "  ", StockDirection.Flat);

        Assert.Equal("코스피 7,133.41", StockPresentation.Line(quote));
    }
}

public sealed class StockWatchSettingsTests
{
    [Fact]
    public void ShowsNothingUntilItIsAskedFor()
    {
        var settings = new StockWatchSettings().Normalized();

        Assert.False(settings.Enabled);
        Assert.Empty(settings.Items);
        Assert.False(settings.IsActive);
    }

    [Fact]
    public void StaysIdleWhenTurnedOnWithAnEmptyList()
    {
        Assert.False(new StockWatchSettings { Enabled = true }.IsActive);
    }

    [Fact]
    public void DropsBlanksAndRepeatsAndTrims()
    {
        var settings = new StockWatchSettings
        {
            Items =
            [
                new WatchedStock { Code = "  005930  ", Name = "  삼성전자  " },
                new WatchedStock { Code = "005930", Name = "삼성전자" },
                new WatchedStock { Code = "   ", Name = "이름만" }
            ]
        }.Normalized();

        var only = Assert.Single(settings.Items);
        Assert.Equal("005930", only.Code);
        Assert.Equal("삼성전자", only.Name);
    }

    [Fact]
    public void CapsTheList()
    {
        var settings = new StockWatchSettings
        {
            Items = [.. Enumerable.Range(0, StockWatchSettings.MaximumItems + 5)
                .Select(index => new WatchedStock { Code = $"00{index:D4}", Name = $"종목{index}" })]
        }.Normalized();

        Assert.Equal(StockWatchSettings.MaximumItems, settings.Items.Length);
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(9000, 600)]
    public void KeepsTheRefreshWithinSensibleBounds(int stored, int expected)
    {
        Assert.Equal(expected, new StockWatchSettings { RefreshSeconds = stored }.Normalized().RefreshSeconds);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(900, 60)]
    public void KeepsTheRotationWithinSensibleBounds(int stored, int expected)
    {
        Assert.Equal(expected, new StockWatchSettings { RotationSeconds = stored }.Normalized().RotationSeconds);
    }

    [Fact]
    public void CarriesTheKindOverFromASearchResult()
    {
        var index = WatchedStock.From(new StockMatch("KOSPI", "코스피", true, "국내지수"));
        var share = WatchedStock.From(new StockMatch("005930", "삼성전자", false, "코스피"));

        Assert.True(index.IsIndex);
        Assert.False(share.IsIndex);
        Assert.Equal("코스피", index.Name);
    }
}
