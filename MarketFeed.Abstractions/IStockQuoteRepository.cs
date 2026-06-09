namespace MarketFeed.Abstractions;

public interface IStockQuoteRepository
{
    /// <summary>
    /// Persists a batch of quotes, skipping duplicates by their (exchange, quote id) key.
    /// </summary>
    /// <returns>
    /// The number of quotes actually inserted into storage — i.e. excluding rows dropped as duplicates.
    /// </returns>
    Task<int> SaveAsync(IReadOnlyList<IStockQuote> stockQuotes, CancellationToken cancellationToken);
}