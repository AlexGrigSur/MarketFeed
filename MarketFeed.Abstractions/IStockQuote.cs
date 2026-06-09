namespace MarketFeed.Abstractions;

public interface IStockQuote
{
    string ExchangeName { get; }
    string QuoteId { get; }
    string Ticker { get; }
    decimal Price { get; }
    long Volume { get; }
    DateTime Timestamp { get; }
}