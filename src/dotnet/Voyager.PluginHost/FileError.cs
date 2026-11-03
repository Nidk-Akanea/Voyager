namespace Voyager.PluginHost;

/// <summary>
/// What the logs say of an error of the file system: its kind and its code. Never its message, which holds paths, the
/// name of the user's account and names of files a package chose (spec of the plugin management, rule 6 of the
/// guarantees).
/// </summary>
internal static class FileError
{
    #region Fields

    // What Windows says of a file another process holds.
    private const int ERROR_SHARING_VIOLATION = 32;
    private const int ERROR_LOCK_VIOLATION = 33;

    // What Linux and macOS say of a file whose lock another process holds (EWOULDBLOCK), which .NET passes on as it is.
    private const int LINUX_WOULD_BLOCK = 11;
    private const int MACOS_WOULD_BLOCK = 35;

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
        OperatingSystem.IsWindows()
            ? (exception.HResult & 0xFFFF) is ERROR_SHARING_VIOLATION or ERROR_LOCK_VIOLATION
            : exception.HResult is LINUX_WOULD_BLOCK or MACOS_WOULD_BLOCK;

    #endregion Public Methods
}
