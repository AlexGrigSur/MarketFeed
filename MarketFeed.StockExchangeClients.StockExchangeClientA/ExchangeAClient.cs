using MarketFeed.Abstractions;
using MarketFeed.StockExchangeClients.Common;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text.Json;

namespace MarketFeed.StockExchangeClients.StockExchangeClientA;

public sealed class ExchangeAClient : BaseStockExchangeClient
{
    private readonly string[] _tickers;

    public ExchangeAClient(ExchangeAClientOptions options, ILogger<ExchangeAClient> logger, IClientMetrics metrics)
        : base(options, logger, metrics)
    {
        _tickers = options.Tickers;
    }

    protected override Task SubscribeAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new { tickers = _tickers });
        return ws.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, ct);
    }

    protected override bool TryParse(ReadOnlySpan<byte> rawMessage, out IStockQuote stockQuote)
    {
        stockQuote = default!;

        try
        {
            var quote = JsonSerializer.Deserialize<ExchangeAStockQuote>(rawMessage);

            if (quote is null || string.IsNullOrEmpty(quote.QuoteId) || string.IsNullOrEmpty(quote.Ticker))
            {
                return false;
            }

            stockQuote = quote;
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "{Exchange}: failed to parse message. Exception: ", InstanceName);
            return false;
        }
    }
}