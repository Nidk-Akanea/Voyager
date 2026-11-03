namespace Voyager.PluginHost;

/// <summary>
/// What the logs say of an error of the file system: its kind and its code. Never its message, which holds paths, the
/// name of the user's account and names of files a package chose (spec of the plugin management, rule 6 of the
/// guarantees).
/// </summary>
internal static class FileError
{
    #region Fields

    private const int ERROR_LOCK_VIOLATION = 33;

    // The Windows codes .NET also gives on other systems for a file another process holds.
    private const int ERROR_SHARING_VIOLATION = 32;

    #endregion Fields

    #region Public Methods

    /// <summary>
    /// The kind and the code of an error, such as <c>IOException (0x80070020)</c>.
    /// </summary>
    /// <param name="exception">The error of the file system.</param>
    public static string Describe(Exception exception) => $"{exception.GetType().Name} (0x{exception.HResult:X8})";

    /// <summary>
    /// Whether an error says that another program holds the file, and lets nobody else open it.
    /// </summary>
    /// <param name="exception">The error of the file system.</param>
    public static bool IsSharingViolation(IOException exception) =>
        (exception.HResult & 0xFFFF) is ERROR_SHARING_VIOLATION or ERROR_LOCK_VIOLATION;

    #endregion Public Methods
}