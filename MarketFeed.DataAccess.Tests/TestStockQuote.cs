using MarketFeed.Abstractions;

namespace MarketFeed.DataAccess.Tests;

internal sealed record TestStockQuote(
    string ExchangeName,
    string QuoteId,
    string Ticker,
    decimal Price,
    long Volume,
    DateTime Timestamp) : IStockQuote;
