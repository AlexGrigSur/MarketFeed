namespace MarketFeed.StockExchangeClients.Common;

public abstract class BaseStockExchangeClientConfiguration
{
    public string InstanceName { get; set; } = "";
    public string Endpoint { get; set; } = "";

    /// <summary>
    /// Initial delay before the first reconnect attempt
    /// </summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Upper bound for the exponential reconnect backoff
    /// </summary>
    public TimeSpan MaxReconnectDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Max reconnect attempts before giving up (default: unlimited)
    /// </summary>
    public int MaxRetryAttempts { get; set; } = int.MaxValue;

    /// <summary>
    /// Drop the connection if no data arrives within this window
    /// </summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
