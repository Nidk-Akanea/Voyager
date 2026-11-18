using Voyager.Persistence;

namespace Voyager.GroupServer;

/// <summary>
/// Brings the database up to date when the server starts.
/// </summary>
internal static class DatabaseStartup
{
    #region Public Methods

    /// <summary>
    /// Applies the pending migrations. A database that cannot be reached, or a migration that fails, does not stop the
    /// server: the diagnostics report the database unavailable (<see cref="DatabaseHealth"/> also checks that no migration
    /// is pending), and what needs it fails until the server restarts.
    /// </summary>
    /// <param name="app">The web application, not started yet.</param>
    /// <returns>A task that ends once the migrations are applied, or have failed.</returns>
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        try
        {
            await app.Services.MigrateVoyagerDatabaseAsync(app.Lifetime.ApplicationStopping);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            GroupServerLog.DatabaseNotMigrated(app.Logger, exception.GetType().Name);
        }
    }

    #endregion Public Methods
}
