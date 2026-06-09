using MarketFeed.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MarketFeed.UnitTests.Fakes;

internal sealed class FakeStockClient : IStockExchangeClient
{
    private readonly IReadOnlyList<IStockQuote> _quotes;

    public string InstanceName { get; }

    public FakeStockClient(string instanceName, IReadOnlyList<IStockQuote> quotes)
    {
        InstanceName = instanceName;
        _quotes = quotes;
    }

    public async Task StartAsync(ChannelWriter<IStockQuote> writer, CancellationToken cancellationToken)
    {
        foreach (var quote in _quotes)
        {
            await writer.WriteAsync(quote, cancellationToken);
        }
    }

    public void Dispose() { }
}