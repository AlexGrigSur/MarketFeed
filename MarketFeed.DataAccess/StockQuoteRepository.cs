using MarketFeed.Abstractions;
using Npgsql;
using NpgsqlTypes;

namespace MarketFeed.DataAccess;

public class StockQuoteRepository : BaseDataMapper, IStockQuoteRepository
{
    private const string CreateTempStockQuoteTableSql = "CREATE TEMP TABLE stock_quotes_temp (LIKE stock_quotes) ON COMMIT DROP;";
    private const string CopyTempStockQuotesSql = "COPY stock_quotes_temp (exchange, quote_id, ticker, price, volume, exchange_ts) FROM STDIN (FORMAT BINARY)";

    private const string InsertStockQuoteSql =
    """
    INSERT INTO stock_quotes (exchange, quote_id, ticker, price, volume, exchange_ts)
    SELECT exchange, quote_id, ticker, price, volume, exchange_ts FROM stock_quotes_temp
    ON CONFLICT (exchange, quote_id) DO NOTHING;
    """;

    public StockQuoteRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<int> SaveAsync(IReadOnlyList<IStockQuote> stockQuotes, CancellationToken cancellationToken)
    {
        if (stockQuotes.Count == 0)
        {
            return 0;
        }

        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await ExecuteNonQueryAsync(cancellationToken, connection, transaction, CreateTempStockQuoteTableSql);
            await BulkCopyAsync(cancellationToken, connection, CopyTempStockQuotesSql, BulkCopyStockQuoteAsync, stockQuotes);
            var insertedCount = await ExecuteNonQueryAsync(cancellationToken, connection, transaction, InsertStockQuoteSql);

            await transaction.CommitAsync(cancellationToken);

            return insertedCount;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);

            if (ex is NpgsqlException { IsTransient: true } or TimeoutException)
            {
                throw new TransientStorageException("Transient failure while saving stock quotes.", ex);
            }

            throw;
        }
    }

    private static async Task BulkCopyStockQuoteAsync(NpgsqlBinaryImporter importer, IStockQuote quote, CancellationToken cancellationToken)
    {
        await importer.WriteAsync(quote.ExchangeName, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteAsync(quote.QuoteId, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteAsync(quote.Ticker, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteAsync(quote.Price, NpgsqlDbType.Numeric, cancellationToken);
        await importer.WriteAsync(quote.Volume, NpgsqlDbType.Bigint, cancellationToken);
        await importer.WriteAsync(quote.Timestamp, NpgsqlDbType.TimestampTz, cancellationToken);
    }
}