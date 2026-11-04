using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Voyager.Persistence.PostgreSql;

/// <summary>
/// Registers the PostgreSQL database of the group server (ADR-0004, ADR-0018).
/// </summary>
public static class PostgreSqlPersistenceExtensions
{
    #region Public Methods

    /// <summary>
    /// Registers <see cref="VoyagerDbContext"/> on PostgreSQL. The PostgreSQL migrations live in this assembly.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The connection string of the database (Aspire gives it as <c>ConnectionStrings:voyager</c>).</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentException">The connection string is empty or not valid; the message never repeats it, since it may hold a password.</exception>
    public static IServiceCollection AddVoyagerPostgreSql(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Checked now, so that a mistake stops the server at startup instead of failing every call later.
        try
        {
            _ = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            // Without the inner exception: its message may quote the connection string, password included.
            throw new ArgumentException("The connection string of the database is not valid.", nameof(connectionString));
        }

        services.AddDbContext<VoyagerDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsAssembly(typeof(PostgreSqlPersistenceExtensions).Assembly.GetName().Name)));

        return services;
    }

    #endregion Public Methods
}
