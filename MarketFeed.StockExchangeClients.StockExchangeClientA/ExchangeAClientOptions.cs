using MarketFeed.StockExchangeClients.Common;

namespace MarketFeed.StockExchangeClients.StockExchangeClientA;

public class ExchangeAClientOptions : BaseStockExchangeClientConfiguration
{
    /// <summary>
    /// Tickers to subscribe to. Empty means "all"
    /// </summary>
    public string[] Tickers { get; set; } = [];
}
