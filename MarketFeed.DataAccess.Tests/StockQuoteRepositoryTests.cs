using FluentAssertions;
using MarketFeed.Abstractions;
using MarketFeed.DataAccess.PostgreSQL;
using Npgsql;

namespace MarketFeed.DataAccess.Tests;

[TestFixture]
public sealed class StockQuoteRepositoryTests
{
    private static readonly DateTime SampleTimestamp = new(2026, 6, 7, 12, 0, 0, DateTimeKind.Utc);

    private StockQuoteRepository _repository = null!;

    [SetUp]
    public async Task SetUp()
    {
        _repository = new StockQuoteRepository(PostgresFixture.ConnectionString);
        await TruncateAsync();
    }

    [Test]
    public async Task SaveAsync_persists_all_fields()
    {
        var quote = new TestStockQuote("ExchangeA", "A-1", "AAPL", 123.45m, 1000, SampleTimestamp);

        var inserted = await _repository.SaveAsync(new[] { quote }, CancellationToken.None);

        inserted.Should().Be(1);
        var rows = await ReadAllAsync();
        rows.Should().ContainSingle();
        rows[0].Should().Be(new StoredRow("ExchangeA", "A-1", "AAPL", 123.45m, 1000, SampleTimestamp));
    }

    [Test]
    public async Task SaveAsync_is_idempotent_for_the_same_key_across_calls()
    {
        var quote = new TestStockQuote("ExchangeA", "A-1", "AAPL", 123.45m, 1000, SampleTimestamp);

        var first = await _repository.SaveAsync(new[] { quote }, CancellationToken.None);
        var second = await _repository.SaveAsync(new[] { quote }, CancellationToken.None);

        first.Should().Be(1);
        second.Should().Be(0);
        (await CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task SaveAsync_deduplicates_within_a_single_batch()
    {
        var quote = new TestStockQuote("ExchangeA", "A-1", "AAPL", 123.45m, 1000, SampleTimestamp);

        var inserted = await _repository.SaveAsync(new[] { quote, quote }, CancellationToken.None);

        inserted.Should().Be(1);
        (await CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task SaveAsync_returns_only_the_count_of_newly_inserted_rows()
    {
        var existing = new TestStockQuote("ExchangeA", "A-1", "AAPL", 1m, 1, SampleTimestamp);
        var fresh = new TestStockQuote("ExchangeA", "A-2", "MSFT", 2m, 2, SampleTimestamp);
        await _repository.SaveAsync(new[] { existing }, CancellationToken.None);

        var inserted = await _repository.SaveAsync(new[] { existing, fresh }, CancellationToken.None);

        inserted.Should().Be(1);
        (await CountAsync()).Should().Be(2);
    }

    [Test]
    public async Task SaveAsync_keeps_distinct_keys()
    {
        var a = new TestStockQuote("ExchangeA", "A-1", "AAPL", 1m, 1, SampleTimestamp);
        var b = new TestStockQuote("ExchangeB", "B-1", "MSFT", 2m, 2, SampleTimestamp);

        var inserted = await _repository.SaveAsync(new[] { a, b }, CancellationToken.None);

        inserted.Should().Be(2);
        (await CountAsync()).Should().Be(2);
    }

    [Test]
    public async Task SaveAsync_treats_same_quote_id_on_different_exchanges_as_distinct()
    {
        var a = new TestStockQuote("ExchangeA", "shared-id", "AAPL", 1m, 1, SampleTimestamp);
        var b = new TestStockQuote("ExchangeB", "shared-id", "MSFT", 2m, 2, SampleTimestamp);

        var inserted = await _repository.SaveAsync(new[] { a, b }, CancellationToken.None);

        inserted.Should().Be(2);
        (await CountAsync()).Should().Be(2);
    }

    [Test]
    public async Task SaveAsync_with_an_empty_batch_is_a_noop()
    {
        var inserted = await _repository.SaveAsync(Array.Empty<IStockQuote>(), CancellationToken.None);

        inserted.Should().Be(0);
        (await CountAsync()).Should().Be(0);
    }

    private static async Task TruncateAsync()
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("TRUNCATE stock_quotes", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountAsync()
    {
        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM stock_quotes", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<List<StoredRow>> ReadAllAsync()
    {
        var rows = new List<StoredRow>();

        await using var connection = new NpgsqlConnection(PostgresFixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT exchange, quote_id, ticker, price, volume, exchange_ts FROM stock_quotes ORDER BY quote_id", connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add(new StoredRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetInt64(4),
                reader.GetFieldValue<DateTime>(5)));
        }

        return rows;
    }

    private sealed record StoredRow(string Exchange, string QuoteId, string Ticker, decimal Price, long Volume, DateTime Timestamp);
}
