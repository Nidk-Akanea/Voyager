using Microsoft.EntityFrameworkCore;

using Npgsql;

using Voyager.Core.Trips;
using Voyager.Persistence.PostgreSql;

namespace Voyager.Persistence.Tests.Support;

/// <summary>
/// A database of its own on the shared server, migrated like the real one and dropped at the end. Each operation made
/// through <see cref="Travelers"/> and <see cref="Trips"/> has its own <see cref="VoyagerDbContext"/>, as each request
/// of the server has: two calls started together are two members at the same time.
/// </summary>
internal sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _name;

    private TestDatabase(string name, string connectionString)
    {
        _name = name;
        ConnectionString = connectionString;
        Travelers = new PerCallTravelerRepository(this);
        Trips = new PerCallTripRepository(this);
    }

    /// <summary>The connection string of this database.</summary>
    public string ConnectionString { get; }

    /// <summary>The travelers, each call on a new context.</summary>
    public ITravelerRepository Travelers { get; }

    /// <summary>The trips, each call on a new context.</summary>
    public ITripRepository Trips { get; }

    /// <summary>Creates a database with a unique name and applies the migrations.</summary>
    /// <returns>The database.</returns>
    public static async Task<TestDatabase> CreateAsync()
    {
        var name = $"voyager_test_{Guid.NewGuid():N}";
        await ExecuteOnServerAsync($"CREATE DATABASE \"{name}\"");

        var connectionString = new NpgsqlConnectionStringBuilder(PostgreSqlServer.ConnectionString) { Database = name }.ConnectionString;
        var database = new TestDatabase(name, connectionString);
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        return database;
    }

    /// <summary>A new context, as a request of the server gets.</summary>
    /// <returns>The context; the caller disposes it.</returns>
    public VoyagerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<VoyagerDbContext>()
            .UseNpgsql(
                ConnectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(PostgreSqlPersistenceExtensions).Assembly.GetName().Name))
            .Options);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await ExecuteOnServerAsync($"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)");
    }

    /// <summary>Runs a query that gives one column, with plain SQL: what the tables really hold.</summary>
    /// <param name="sql">The query.</param>
    /// <returns>The first column of every row, as text.</returns>
    public async Task<IReadOnlyList<string>> ColumnAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private static async Task ExecuteOnServerAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(PostgreSqlServer.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
