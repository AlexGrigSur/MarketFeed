using Npgsql;
using Testcontainers.PostgreSql;

namespace MarketFeed.DataAccess.Tests;

[SetUpFixture]
public sealed class PostgresFixture
{
    private static readonly PostgreSqlContainer Container = new PostgreSqlBuilder("postgres:16")
        .Build();

    public static string ConnectionString { get; private set; } = string.Empty;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        await Container.StartAsync();
        ConnectionString = Container.GetConnectionString();
        await ApplySchemaAsync();
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        await Container.DisposeAsync();
    }

    private static async Task ApplySchemaAsync()
    {
        var schema = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "init.sql"));

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(schema, connection);
        await command.ExecuteNonQueryAsync();
    }
}
