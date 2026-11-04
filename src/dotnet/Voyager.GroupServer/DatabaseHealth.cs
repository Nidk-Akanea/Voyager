using Voyager.Persistence;

namespace Voyager.GroupServer;

/// <summary>
/// Tells whether the server reaches its PostgreSQL database, for the diagnostics. Nothing is migrated yet: the first
/// migration comes with the first brick of the core.
/// </summary>
/// <param name="scopeFactory">Gives the database context, which lives in a request scope; none without a connection string.</param>
/// <param name="logger">Records why the database could not be reached, without the connection string.</param>
public sealed partial class DatabaseHealth(IServiceScopeFactory scopeFactory, ILogger<DatabaseHealth> logger)
{
    #region Fields

    // A diagnostic must answer quickly, even when the database host does not answer at all.
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(3);

    #endregion Fields

    #region Private Methods

    [LoggerMessage(Level = LogLevel.Warning, Message = "The database could not be reached: {Error}")]
    private static partial void LogUnreachable(ILogger logger, string error);

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
            return await database.Database.CanConnectAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogUnreachable(logger, "no answer in time");
            return false;
        }
    }

    #endregion Public Methods
}
