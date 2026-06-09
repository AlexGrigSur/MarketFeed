using Npgsql;
using NpgsqlTypes;
using System.Data;
using System.Runtime.CompilerServices;

namespace MarketFeed.DataAccess.PostgreSQL;

public abstract class BaseDataMapper
{
    private readonly string _connectionString;

    protected BaseDataMapper(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected async Task<int> ExecuteNonQueryAsync(
        CancellationToken cancellationToken,
        string sql,
        params (string name, object value)[] sqlParameters)
    {
        await using var connection = GetConnection();
        await using var command = CreateCommand(connection, sql, sqlParameters);

        connection.Open();

        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    protected async Task<T> ExecuteReaderAsync<T>(
        CancellationToken cancellationToken,
        string sql,
        Func<NpgsqlDataReader, CancellationToken, Task<T>> readFunc,
        params (string name, object value)[] sqlParameters)
    {
        await using var connection = GetConnection();
        await using var command = CreateCommand(connection, sql, sqlParameters);

        connection.Open();

        try
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await readFunc(reader, cancellationToken);
        }
        finally
        {
            connection.Close();
        }
    }

    protected async Task BulkCopyAsync<T>(
        CancellationToken cancellationToken,
        NpgsqlConnection connection,
        string sqlQuery,
        Func<NpgsqlBinaryImporter, T, CancellationToken, Task> bulkCopyEntityAsync,
        IEnumerable<T> entities)
    {
        await using var importer = await connection.BeginBinaryImportAsync(sqlQuery, cancellationToken);
        foreach (var entity in entities)
        {
            await importer.StartRowAsync(cancellationToken);
            await bulkCopyEntityAsync(importer, entity, cancellationToken);
        }
        await importer.CompleteAsync();
    }

    #region transaction

    protected async Task<int> ExecuteNonQueryAsync(
        CancellationToken cancellationToken,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sqlQuery,
        params (string name, object value)[] sqlParameters)
    {
        CheckConnectionOpen(connection);
        await using var command = CreateCommand(connection, sqlQuery, sqlParameters, transaction);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    protected async Task<T> ExecuteReaderAsync<T>(
        CancellationToken cancellationToken,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sqlQuery,
        Func<IDataReader, T> readAction,
        params (string name, object value)[] sqlParameters)
    {
        CheckConnectionOpen(connection);
        await using var command = CreateCommand(connection, sqlQuery, sqlParameters, transaction);
        var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = readAction(reader);
        return result;
    }

    #endregion

    protected NpgsqlConnection GetConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }

    private static NpgsqlCommand CreateCommand(
        NpgsqlConnection connection,
        string query,
        IEnumerable<(string name, object value)> parameters,
        NpgsqlTransaction transaction = null!)
    {
        var command = connection.CreateCommand();
        command.CommandText = query;
        command.Transaction = transaction;

        if (parameters == null!)
        {
            return command;
        }

        foreach (var (parameter, value) in parameters)
        {
            command.Parameters.AddWithValue(parameter, value ?? DBNull.Value);
        }

        return command;
    }

    private static void CheckConnectionOpen(NpgsqlConnection connection)
    {
        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Connection must be open");
        }
    }
}