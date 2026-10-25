using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Voyager.Persistence;

/// <summary>
/// Brings the Voyager database up to date when a host starts.
/// </summary>
public static class DatabaseMigrationExtensions
{
    #region Public Methods

    /// <summary>
    /// Creates the database if it does not exist yet, then applies the pending EF Core migrations.
    /// </summary>
    /// <param name="services">The host's service provider.</param>
    /// <param name="cancellationToken">Stops the migration when the host shuts down.</param>
    public static async Task MigrateVoyagerDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<VoyagerDbContext>();
        await database.Database.MigrateAsync(cancellationToken);
    }

    #endregion Public Methods
}
