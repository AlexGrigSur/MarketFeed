namespace MarketFeed.Host.Settings;

public sealed class StockExchangeProcessorConfiguration
{
    /// <summary>
    /// Bounded capacity of the in-memory quote channel
    /// </summary>
    public int ChannelMaxSize { get; set; } = 10000;

    /// <summary>
    /// Number of quotes accumulated before a batch is persisted
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// How often an under-full batch is saved
    /// </summary>
    public TimeSpan SaveInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Retry policy for persisting a batch (only transient storage failures are retried)
    /// </summary>
    public SaveRetryConfiguration SaveRetry { get; set; } = new();
}