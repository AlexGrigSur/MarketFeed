using MarketFeed.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketFeed.UnitTests.Fakes;

internal sealed class FakeStockQuoteRepository : IStockQuoteRepository
{
    private readonly object _lock = new();
    private readonly List<IStockQuote[]> _saved = new();
    private int _attempts;

    public Func<int, Exception?>? FaultSelector { get; set; }

    public int Attempts
    {
        get
        {
            lock (_lock)
            {
                return _attempts;
            }
        }
    }

    public int TotalSaved
    {
        get
        {
            lock (_lock)
            {
                return _saved.Sum(batch => batch.Length);
            }
        }
    }

    public IReadOnlyList<IStockQuote[]> Saved
    { 
        get
        {
            lock (_lock)
            {
                return _saved.ToArray();
            }
        }
    }

    public Task<int> SaveAsync(IReadOnlyList<IStockQuote> stockQuotes, CancellationToken cancellationToken)
    {
        int attempt;
        lock (_lock)
        {
            attempt = ++_attempts;
        }

        if (FaultSelector?.Invoke(attempt) is { } fault)
        {
            return Task.FromException<int>(fault);
        }

        var copy = stockQuotes.ToArray();
        lock (_lock)
        {
            _saved.Add(copy);
        }
        return Task.FromResult(copy.Length);
    }
}