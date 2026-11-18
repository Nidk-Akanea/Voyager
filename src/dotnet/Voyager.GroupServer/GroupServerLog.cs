namespace Voyager.GroupServer;

/// <summary>
/// Log messages of the group server host itself (Program.cs).
/// </summary>
internal static partial class GroupServerLog
{
    #region Public Methods

    [LoggerMessage(Level = LogLevel.Warning, Message = "The development identity is on: every call is accepted without signing in, on behalf of the development account and group")]
    public static partial void DevelopmentIdentityOn(ILogger logger);

    // The kind of the failure only, never its message: it may quote the connection string, password included.
    [LoggerMessage(Level = LogLevel.Error, Message = "The database could not be brought up to date ({FailureType}): what needs it fails until the server restarts")]
    public static partial void DatabaseNotMigrated(ILogger logger, string failureType);

    // The name of the connection string only, never the string itself: it may hold a password.
    [LoggerMessage(Level = LogLevel.Warning, Message = "No connection string named {ConnectionName} is configured: the database is reported unavailable")]
    public static partial void NoDatabase(ILogger logger, string connectionName);

    [LoggerMessage(Level = LogLevel.Critical, Message = "The group server failed and stops")]
    public static partial void ServerFailed(ILogger logger, Exception exception);

    #endregion Public Methods
}
