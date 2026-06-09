using MarketFeed.Abstractions;
using MarketFeed.Host.Enums;
using MarketFeed.Host.Settings;
using MarketFeed.StockExchangeClients.StockExchangeClientA;
using MarketFeed.StockExchangeClients.StockExchangeClientB;

namespace MarketFeed.Host;

public sealed class StockExchangeClientFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IClientMetrics _metrics;

    public StockExchangeClientFactory(ILoggerFactory loggerFactory, IClientMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(metrics);

        _loggerFactory = loggerFactory;
        _metrics = metrics;
    }

    public IStockExchangeClient CreateClient(StockClientConfiguration configuration) => configuration.ExchangeType switch
    {
        ExchangeType.ExchangeA => new ExchangeAClient(ToOptionsA(configuration), _loggerFactory.CreateLogger<ExchangeAClient>(), _metrics),
        ExchangeType.ExchangeB => new ExchangeBClient(ToOptionsB(configuration), _loggerFactory.CreateLogger<ExchangeBClient>(), _metrics),
        _ => throw new ArgumentOutOfRangeException(nameof(configuration), configuration.ExchangeType, "Unknown exchange type.")
    };

    private static ExchangeAClientOptions ToOptionsA(StockClientConfiguration c) => new()
    {
        InstanceName = c.InstanceName,
        Endpoint = c.Endpoint,
        Tickers = c.Tickers,
        ReconnectDelay = c.ReconnectDelay,
        MaxReconnectDelay = c.MaxReconnectDelay,
        MaxRetryAttempts = c.MaxRetryAttempts,
        IdleTimeout = c.IdleTimeout,
    };

    private static ExchangeBClientOptions ToOptionsB(StockClientConfiguration c) => new()
    {
        InstanceName = c.InstanceName,
        Endpoint = c.Endpoint,
        Tickers = c.Tickers,
        AuthToken = c.AuthToken ?? "",
        ReconnectDelay = c.ReconnectDelay,
        MaxReconnectDelay = c.MaxReconnectDelay,
        MaxRetryAttempts = c.MaxRetryAttempts,
        IdleTimeout = c.IdleTimeout,
    };
}