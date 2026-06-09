namespace MarketFeed.Host.Settings;

public sealed class MetricsConfiguration
{
    /// <summary>Port for the dedicated Prometheus metrics endpoint (/metrics).</summary>
    public int Port { get; set; } = 9100;
}
