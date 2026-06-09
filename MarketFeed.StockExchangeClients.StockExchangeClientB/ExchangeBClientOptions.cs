using MarketFeed.StockExchangeClients.Common;

namespace MarketFeed.StockExchangeClients.StockExchangeClientB;

public class ExchangeBClientOptions : BaseStockExchangeClientConfiguration
{
    public string AuthToken { get; set; } = "secret-token";

    /// <summary>
    /// Tickers to subscribe to. Empty means "all"
    /// </summary>
    public string[] Tickers { get; set; } = [];
}