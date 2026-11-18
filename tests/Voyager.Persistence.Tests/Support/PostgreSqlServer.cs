using Testcontainers.PostgreSql;

namespace Voyager.Persistence.Tests.Support;

/// <summary>
/// The one PostgreSQL server (a throwaway container) the whole test run shares: starting one per class would be slow.
/// Each test class gets its own database on it, see <see cref="TestDatabase"/>.
/// </summary>
[TestClass]
public static class PostgreSqlServer
{
    // The image Aspire runs in development (PostgreSQL with PostGIS).
    private const string IMAGE = "postgis/postgis:18-3.6";

    private static PostgreSqlContainer? _container;

    /// <summary>The connection string of the server, on its default database. Throwaway values only.</summary>
    internal static string ConnectionString =>
        _container?.GetConnectionString() ?? throw new InvalidOperationException("The PostgreSQL container is not started.");

    [AssemblyCleanup]
    public static async Task StopAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            _container = null;
        }
    }

    [AssemblyInitialize]
    public static async Task StartAsync(TestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _container = new PostgreSqlBuilder(IMAGE).Build();
        await _container.StartAsync(context.CancellationToken);
    }
}
