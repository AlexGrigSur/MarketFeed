namespace MarketFeed.Abstractions;

public interface IClientMetrics
{
    /// <summary>
    /// A quote was parsed and queued for processing
    /// </summary>
    void TickReceived(string exchangeName);

    /// <summary>
    /// A connection was re-established after a drop
    /// </summary>
    void Reconnected(string exchangeName);

    /// <summary>
    /// A message arrived but could not be parsed
    /// </summary>
    void ParseError(string exchangeName);

    /// <summary>
    /// The current connection state of a client (true = connected)
    /// </summary>
    void SetConnected(string exchangeName, bool connected);
}