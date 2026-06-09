using MarketFeed.Abstractions;
using System.Text.Json.Serialization;

namespace MarketFeed.StockExchangeClients.StockExchangeClientA;

internal sealed class ExchangeAStockQuote : IStockQuote
{
    private const string Exchange = "ExchangeA";

    [JsonIgnore]
    public string ExchangeName => Exchange;

    [JsonPropertyName("id")]
    public string QuoteId { get; init; } = string.Empty;

    [JsonPropertyName("sym")]
    public string Ticker { get; init; } = string.Empty;

    [JsonPropertyName("px")]
    public decimal Price { get; init; }

    [JsonPropertyName("qty")]
    public long Volume { get; init; }

    [JsonPropertyName("t")]
    public long TimestampEpochMs { get; init; }

    [JsonIgnore]
    public DateTime Timestamp => DateTimeOffset.FromUnixTimeMilliseconds(TimestampEpochMs).UtcDateTime;
}