using Microsoft.Extensions.Options;

using Voyager.Persistence;

namespace Voyager.LocalService;

/// <summary>
/// Prepares the local SQLite database when the service starts.
/// </summary>
internal static partial class LocalDatabaseStartup
{
    #region Private Methods

    // The path is not logged: it holds the user's account name.
    [LoggerMessage(Level = LogLevel.Information, Message = "The local database is ready")]
    private static partial void LogDatabaseReady(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "The local database could not be created or migrated")]
    private static partial void LogPreparationFailed(ILogger logger, Exception exception);

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// Creates the database folder and migrates the database. A failure is logged and does not stop
    /// the service: the "About" screen then shows the database as unavailable, which helps understand
    /// the problem.
    /// </summary>
    /// <param name="app">The web application, not started yet.</param>
    public static async Task PrepareLocalDatabaseAsync(this WebApplication app)
    {
        var databasePath = app.Services.GetRequiredService<IOptions<LocalServiceOptions>>().Value.ResolvedDatabasePath;
        var state = app.Services.GetRequiredService<LocalDatabaseState>();
        try
        {
            var folder = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            await app.Services.MigrateVoyagerDatabaseAsync(app.Lifetime.ApplicationStopping);
            state.IsReady = true;
            LogDatabaseReady(app.Logger);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Startup is a boundary: the service stays up so that the problem can be seen and reported.
            LogPreparationFailed(app.Logger, exception);
        }
    }

    #endregion Public Methods
}
