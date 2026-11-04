namespace Voyager.GroupServer;

/// <summary>
/// Log messages of the group server host itself (Program.cs).
/// </summary>
internal static partial class GroupServerLog
{
    #region Public Methods

    [LoggerMessage(Level = LogLevel.Warning, Message = "The development identity is on: every call is accepted without signing in, on behalf of the development account and group")]
    public static partial void DevelopmentIdentityOn(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "The group server failed and stops")]
    public static partial void ServerFailed(ILogger logger, Exception exception);

    #endregion Public Methods
}
