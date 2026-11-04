using Voyager.Persistence;

namespace Voyager.GroupServer;

/// <summary>
/// Tells whether the server reaches its PostgreSQL database, for the diagnostics. Nothing is migrated yet: the first
/// migration comes with the first brick of the core.
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "The database could not be reached: {Reason}")]
    private static partial void LogUnreachable(ILogger logger, string reason);

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// Whether the database answers within a few seconds. False when the server has no connection string.
    /// </summary>
    /// <param name="cancellationToken">Stops the check when the caller gives up.</param>
    /// <returns>True when a connection could be opened.</returns>
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
            if (await database.Database.CanConnectAsync(timeout.Token))
            {
                return true;
            }

            LogUnreachable(logger, "the connection was refused or failed");
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogUnreachable(logger, "no answer in time");
            return false;
        }
    }

    #endregion Public Methods
}
