using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Voyager.Persistence.Sqlite;

/// <summary>
/// Registers the SQLite database of the local service (ADR-0004).
/// </summary>
public static class SqlitePersistenceExtensions
{
    #region Public Methods

    /// <summary>
    /// Registers <see cref="VoyagerDbContext"/> on a SQLite file, created by SQLite on the first
    /// connection. Its folder must exist. The SQLite migrations live in this assembly, apart from those
    /// of PostgreSQL (ADR-0004).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="databasePath">Gives the path of the database file, resolved when the context is created.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddVoyagerSqlite(this IServiceCollection services, Func<IServiceProvider, string> databasePath)
    {
        services.AddDbContext<VoyagerDbContext>((provider, options) => options.UseSqlite(
            new SqliteConnectionStringBuilder { DataSource = Path.GetFullPath(databasePath(provider)) }.ToString(),
            sqlite => sqlite.MigrationsAssembly(typeof(SqlitePersistenceExtensions).Assembly.GetName().Name)));

        return services;
    }

    #endregion Public Methods
}
