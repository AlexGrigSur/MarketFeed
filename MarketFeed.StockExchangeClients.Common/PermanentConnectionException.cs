using System.Net;

namespace MarketFeed.StockExchangeClients.Common;

/// <summary>
/// A WebSocket handshake rejected with a permanent client error (e.g. 401/403/400).
/// Retrying the connection will not help, so the client should give up.
/// </summary>
public sealed class PermanentConnectionException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public PermanentConnectionException(HttpStatusCode statusCode, Exception innerException)
        : base($"WebSocket handshake rejected with HTTP {(int)statusCode} ({statusCode}); retrying will not help.", innerException)
    {
        StatusCode = statusCode;
    }
}
