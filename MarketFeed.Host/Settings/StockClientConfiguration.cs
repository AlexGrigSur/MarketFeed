using MarketFeed.Host.Enums;
using MarketFeed.StockExchangeClients.Common;

namespace MarketFeed.Host.Settings;

public sealed class StockClientConfiguration : BaseStockExchangeClientConfiguration
{
    public ExchangeType ExchangeType { get; set; }

    /// <summary>
    /// Tickers to subscribe to. Empty means "all"
    /// </summary>
    public string[] Tickers { get; set; } = [];

    public string? AuthToken { get; set; }
}