using MarketFeed.Abstractions;
using MarketFeed.StockExchangeClients.Common;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace MarketFeed.StockExchangeClients.StockExchangeClientB;

public sealed class ExchangeBClient : BaseStockExchangeClient
{
    private static readonly XmlSerializer Serializer = new(typeof(ExchangeBStockQuote));

    private readonly string _authToken;
    private readonly string[] _tickers;

    public ExchangeBClient(ExchangeBClientOptions options, ILogger<ExchangeBClient> logger, IClientMetrics metrics)
        : base(options, logger, metrics)
    {
        _authToken = options.AuthToken;
        _tickers = options.Tickers;
    }

    protected override void ConfigureWebSocket(ClientWebSocketOptions options)
    {
        options.SetRequestHeader("Authorization", $"Bearer {_authToken}");
    }

    protected override Task SubscribeAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var subscribe = new XElement("subscribe", _tickers.Select(t => new XElement("symbol", t)));
        var payload = Encoding.UTF8.GetBytes(subscribe.ToString(SaveOptions.DisableFormatting));
        return ws.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, ct);
    }

    protected override bool TryParse(ReadOnlySpan<byte> rawMessage, out IStockQuote stockQuote)
    {
        stockQuote = default!;

        try
        {
            using var reader = new StringReader(Encoding.UTF8.GetString(rawMessage));

            if (Serializer.Deserialize(reader) is not ExchangeBStockQuote quote
                || string.IsNullOrEmpty(quote.Ticker))
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