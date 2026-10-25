namespace Voyager.LocalService;

/// <summary>
/// Log messages of the local service host itself (Program.cs).
/// </summary>
internal static partial class LocalServiceLog
{
    #region Public Methods

    [LoggerMessage(Level = LogLevel.Critical, Message = "The local service failed and stops")]
    public static partial void ServiceFailed(ILogger logger, Exception exception);

    #endregion Public Methods
}
