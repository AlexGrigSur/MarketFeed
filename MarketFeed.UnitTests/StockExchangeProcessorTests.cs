using FluentAssertions;
using MarketFeed.Abstractions;
using MarketFeed.Host;
using MarketFeed.Host.Settings;
using MarketFeed.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;

namespace MarketFeed.UnitTests;

[TestFixture]
public sealed class StockExchangeProcessorTests
{
    private static readonly TimeSpan NeverSave = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);
    private static readonly DateTime Ts = new(2026, 6, 7, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Saves_a_full_batch_test()
    {
        var repository = new FakeStockQuoteRepository();
        var clients = new[] { new FakeStockClient("ExchangeA", MakeQuotes("ExchangeA", 5)) };
        using var processor = CreateProcessor(clients, repository, batchSize: 5, SaveInterval: NeverSave);

        await RunUntilAsync(processor, () => repository.TotalSaved >= 5);

        repository.Saved.Should().ContainSingle();
        repository.Saved[0].Should().HaveCount(5);
    }

    [Test]
    public async Task Saves_a_partial_batch_on_the_timer_test()
    {
        var repository = new FakeStockQuoteRepository();
        var clients = new[] { new FakeStockClient("ExchangeA", MakeQuotes("ExchangeA", 3)) };
        using var processor = CreateProcessor(clients, repository, batchSize: 100, SaveInterval: TimeSpan.FromMilliseconds(200));

        await RunUntilAsync(processor, () => repository.TotalSaved >= 3);

        repository.TotalSaved.Should().Be(3);
    }

    [Test]
    public async Task Merges_quotes_from_multiple_clients_test()
    {
        var repository = new FakeStockQuoteRepository();
        var clients = new[]
        {
            new FakeStockClient("ExchangeA", MakeQuotes("ExchangeA", 2)),
            new FakeStockClient("ExchangeB", MakeQuotes("ExchangeB", 2)),
        };
        using var processor = CreateProcessor(clients, repository, batchSize: 4, SaveInterval: NeverSave);

        await RunUntilAsync(processor, () => repository.TotalSaved >= 4);

        var savedExchanges = repository.Saved.SelectMany(batch => batch).Select(quote => quote.ExchangeName);
        savedExchanges.Should().Contain(new[] { "ExchangeA", "ExchangeB" });
    }

    [Test]
    public async Task Drains_buffered_quotes_on_shutdown_test()
    {
        var repository = new FakeStockQuoteRepository();
        var clients = new[] { new FakeStockClient("ExchangeA", MakeQuotes("ExchangeA", 3)) };
        using var processor = CreateProcessor(clients, repository, batchSize: 1000, SaveInterval: NeverSave);

        await processor.StartAsync(CancellationToken.None);
        await processor.StopAsync(CancellationToken.None);

        repository.TotalSaved.Should().Be(3);
    }

    [Test]
    public async Task Drops_a_permanently_failing_batch_and_keeps_processing_test()
    {
        var repository = new FakeStockQuoteRepository
        {
            FaultSelector = attempt => attempt == 1 ? new InvalidOperationException("permanent") : null,
        };
        var clients = new[] { new FakeStockClient("ExchangeA", MakeQuotes("ExchangeA", 4)) };
        using var processor = CreateProcessor(clients, repository, batchSize: 2, SaveInterval: NeverSave);

        await RunUntilAsync(processor, () => repository.TotalSaved >= 2);

        repository.TotalSaved.Should().Be(2);
        repository.Attempts.Should().Be(2);
    }

    [Test]
    public async Task Retries_a_transiently_failing_batch_until_it_succeeds_test()
    {
        var repository = new FakeStockQuoteRepository
        {
            FaultSelector = attempt => attempt <= 2 ? new TransientStorageException("transient", new Exception()) : null,
        };
        var clients = new[] { new FakeStockClient("ExchangeA", MakeQuotes("ExchangeA", 2)) };
        using var processor = CreateProcessor(clients, repository, batchSize: 2, SaveInterval: NeverSave);

        await RunUntilAsync(processor, () => repository.TotalSaved >= 2);

        repository.TotalSaved.Should().Be(2);
        repository.Attempts.Should().Be(3);
    }

    private static StockExchangeProcessor CreateProcessor(
        IReadOnlyList<IStockExchangeClient> clients,
        IStockQuoteRepository repository,
        int batchSize,
        TimeSpan SaveInterval)
    {
        var options = new StockExchangeProcessorConfiguration
        {
            BatchSize = batchSize,
            SaveInterval = SaveInterval,
            ChannelMaxSize = 10_000,
        };

        return new StockExchangeProcessor(
            clients,
            repository,
            options,
            new FakeProcessorMetrics(),
            new FakeHostApplicationLifetime(),
            NullLogger<StockExchangeProcessor>.Instance);
    }

    private static async Task RunUntilAsync(StockExchangeProcessor processor, Func<bool> until)
    {
        await processor.StartAsync(CancellationToken.None);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            while (!until() && stopwatch.Elapsed < WaitTimeout)
            {
                await Task.Delay(20);
            }
        }
        finally
        {
            await processor.StopAsync(CancellationToken.None);
        }
    }

    private static IReadOnlyList<IStockQuote> MakeQuotes(string exchangeName, int count)
        => Enumerable.Range(0, count)
            .Select(i => (IStockQuote)new TestStockQuote(exchangeName, $"{exchangeName}-{i}", "AAPL", 100m + i, 10 + i, Ts))
            .ToArray();
}