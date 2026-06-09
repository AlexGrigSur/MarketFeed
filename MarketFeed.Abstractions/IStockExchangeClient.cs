using System.Threading.Channels;

namespace MarketFeed.Abstractions;

public interface IStockExchangeClient : IDisposable
{
    string InstanceName { get; }
    Task StartAsync(ChannelWriter<IStockQuote> writer, CancellationToken cancellationToken);
}