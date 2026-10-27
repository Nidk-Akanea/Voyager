using System.ComponentModel.DataAnnotations;

namespace Voyager.LocalService;

/// <summary>
/// Settings of the local service, read from the "Voyager" configuration section. The desktop app
/// (or Aspire in development) provides them through environment variables such as Voyager__LocalToken.
/// </summary>
public sealed class LocalServiceOptions
{
    #region Fields

    private const int MINIMUM_TOKEN_LENGTH = 32;

    /// <summary>Origin of the desktop app's user interface once packaged (custom protocol).</summary>
    public const string PACKAGED_UI_ORIGIN = "voyager://app";

    /// <summary>The configuration section holding these settings.</summary>
    public const string SECTION = "Voyager";

    #endregion Fields

    #region Properties

    /// <summary>
    /// Origins allowed to call the service from a browser context (CORS). Defaults to the packaged
    /// user interface; the development user interface adds its own origin.
    /// </summary>
    public IReadOnlyList<string> AllowedOrigins { get; set; } = [];

    /// <summary>
    /// Path of the SQLite database. Defaults to <see cref="DefaultDatabasePath"/>, outside the repository.
    /// </summary>
    public string? DatabasePath { get; set; }

    /// <summary>
    /// Secret shared with the desktop app, generated at each launch. Every gRPC call must carry it,
    /// so that no other program on the machine can read the member's trips.
    /// </summary>
    [Required]
    [MinLength(MINIMUM_TOKEN_LENGTH)]
    public string LocalToken { get; set; } = string.Empty;

    /// <summary>
    /// The folder that holds the plugins, one sub-folder each. Set by the desktop app (or Aspire in
    /// development), never by the user: loading assemblies from a folder the user can choose is a risk
    /// that ADR-0015 declines. Without it, no plugin is loaded.
    /// </summary>
    public string? PluginsDirectory { get; set; }

    /// <summary>The database path to use: <see cref="DatabasePath"/>, or else the default one.</summary>
    public string ResolvedDatabasePath => Path.GetFullPath(DatabasePath ?? DefaultDatabasePath());

    /// <summary>
    /// Set by the desktop app, which keeps the service's standard input open while it runs: the
    /// service stops when that input closes, so that it never outlives the app, even after a crash.
    /// </summary>
    public bool StopWhenInputCloses { get; set; }

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// The per-user application data folder, for example %LOCALAPPDATA%\Voyager\voyager.db on Windows.
    /// </summary>
    public static string DefaultDatabasePath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Voyager", "voyager.db");

    #endregion Public Methods
}
