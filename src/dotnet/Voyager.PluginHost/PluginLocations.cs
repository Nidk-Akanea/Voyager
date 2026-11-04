namespace Voyager.PluginHost;

/// <summary>
/// Where the plugins and the files that manage them are, as the desktop app gives them: never chosen by the user
/// (ADR-0015, ADR-0016).
/// </summary>
/// <param name="ShippedDirectory">The folder of the plugins shipped with Voyager; null for none.</param>
/// <param name="UserDirectory">
/// The plugins folder of the user's data: installed plugins, requests and state; null when plugins cannot be managed.
/// </param>
public sealed record PluginLocations(string? ShippedDirectory, string? UserDirectory)
{
    private const string INSTALLED_FOLDER = "installed";
    private const string STAGING_FOLDER = "staging";
    private const string REQUESTS_FILE = "requests.json";
    private const string STATE_FILE = "state.json";
    private const string ACTIVITY_FILE = "activity.json";

    /// <summary>The folder of the installed plugins, one sub-folder each; null when plugins cannot be managed.</summary>
    public string? InstalledDirectory => UserDirectory is null ? null : Path.Combine(UserDirectory, INSTALLED_FOLDER);

    /// <summary>
    /// The private copies of the packages the user chose to install, made by the desktop app (ADR-0016, decision 3);
    /// null when plugins cannot be managed.
    /// </summary>
    public string? StagingDirectory => UserDirectory is null ? null : Path.Combine(UserDirectory, STAGING_FOLDER);

    /// <summary>The requests written by the desktop app; null when plugins cannot be managed.</summary>
    public string? RequestsFile => UserDirectory is null ? null : Path.Combine(UserDirectory, REQUESTS_FILE);

    /// <summary>The state written by the service; null when plugins cannot be managed.</summary>
    public string? StateFile => UserDirectory is null ? null : Path.Combine(UserDirectory, STATE_FILE);

    /// <summary>
    /// The trace of the plugins being loaded or called, written by the service while it runs and deleted when it stops
    /// normally: found at startup, it tells which plugin the service crashed with. Null when plugins cannot be managed.
    /// </summary>
    public string? ActivityFile => UserDirectory is null ? null : Path.Combine(UserDirectory, ACTIVITY_FILE);

    /// <summary>The folders of plugins in the order they are read: shipped first, then installed.</summary>
    public IReadOnlyList<PluginRoot> Roots =>
    [
        .. ShippedDirectory is null ? Array.Empty<PluginRoot>() : [new PluginRoot(ShippedDirectory, PluginOrigin.Shipped)],
        .. InstalledDirectory is null ? Array.Empty<PluginRoot>() : [new PluginRoot(InstalledDirectory, PluginOrigin.Installed)],
    ];

    /// <summary>
    /// The folder of the plugins of an origin.
    /// </summary>
    /// <param name="origin">Shipped or installed.</param>
    /// <returns>Null when there is no such folder.</returns>
    public string? RootOf(PluginOrigin origin) => origin switch
    {
        PluginOrigin.Shipped => ShippedDirectory,
        PluginOrigin.Installed => InstalledDirectory,
        _ => null,
    };
}
