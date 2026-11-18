using Microsoft.EntityFrameworkCore;

using Voyager.Persistence;

namespace Voyager.GroupServer;

/// <summary>
/// Tells whether the server reaches its PostgreSQL database, for the diagnostics. The migrations are applied at startup
/// (<see cref="DatabaseStartup"/>).
/// </summary>
/// <param name="scopeFactory">Gives the database context, which lives in a request scope; none without a connection string.</param>
/// <param name="logger">Records that the database could not be reached, never where it is (EF Core's own connection log is off, appsettings.json).</param>
public sealed partial class DatabaseHealth(IServiceScopeFactory scopeFactory, ILogger<DatabaseHealth> logger)
{
    #region Fields

    // A diagnostic must answer quickly, even when the database host does not answer at all.
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(3);

    #endregion Fields

    #region Private Methods

    [LoggerMessage(Level = LogLevel.Warning, Message = "The database could not be reached: the connection was refused or failed")]
    private static partial void LogUnreachable(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The database answers but is not up to date: its migrations were not applied")]
    private static partial void LogNotMigrated(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The database could not be reached: no answer within {TimeoutMilliseconds} ms")]
    private static partial void LogNoAnswer(ILogger logger, double timeoutMilliseconds);

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// Whether the database answers within a few seconds and is up to date. False when the server has no connection
    /// string, cannot reach the database, or has not applied its migrations.
    /// </summary>
    /// <param name="cancellationToken">Stops the check when the caller gives up.</param>
    /// <returns>True when a connection could be opened and no migration is pending.</returns>
    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetService<VoyagerDbContext>();
        if (database is null)
        {
            return false;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        try
        {
            if (!await database.Database.CanConnectAsync(timeout.Token))
            {
                LogUnreachable(logger);
                return false;
            }

            // Reachable is not usable: a migration that failed at startup leaves the tables missing.
            if ((await database.Database.GetPendingMigrationsAsync(timeout.Token)).Any())
            {
                LogNotMigrated(logger);
                return false;
            }

            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogNoAnswer(logger, _timeout.TotalMilliseconds);
            return false;
        }
    }

    #endregion Public Methods
}
