namespace Voyager.GroupServer;

/// <summary>
/// Settings of the group server, read from the "Voyager" configuration section (environment variables such as
/// <c>Voyager__DevelopmentIdentity</c>). The two development settings are refused outside the Development
/// environment: the server stops rather than run with them.
/// </summary>
public sealed class GroupServerOptions
{
    #region Fields

    /// <summary>The configuration section holding these settings.</summary>
    public const string SECTION = "Voyager";

    #endregion Fields

    #region Properties

    /// <summary>
    /// Accepts calls without signing in, on behalf of the development account and group (ADR-0019, decision 9). Set by
    /// Aspire only, and refused outside the Development environment.
    /// </summary>
    public bool DevelopmentIdentity { get; set; }

    /// <summary>
    /// The oldest version of the desktop app the server still serves, such as <c>0.4.0</c>; empty for any version
    /// (ADR-0018, decision 6). A prerelease suffix is ignored: only major, minor and patch are compared.
    /// </summary>
    public string? MinimumAppVersion { get; set; }

    /// <summary>
    /// The folder of the shipped plugins, as a full path, in development only (Aspire gives <c>artifacts/plugins</c>).
    /// Elsewhere the folder is fixed: <c>plugins</c> next to the server (<c>/app/plugins</c> in the image, ADR-0020,
    /// decision 3), never read from the environment.
    /// </summary>
    public string? ShippedPluginsDirectory { get; set; }

    #endregion Properties
}
