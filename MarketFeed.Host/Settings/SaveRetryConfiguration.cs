namespace MarketFeed.Host.Settings;

public sealed class SaveRetryConfiguration
{
    /// <summary>
    /// Max retry attempts on a transient save failure
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Initial backoff delay before the first retry
    /// </summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Upper bound for the exponential backoff
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(5);
}