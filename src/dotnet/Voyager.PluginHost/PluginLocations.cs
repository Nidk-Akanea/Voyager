namespace Voyager.PluginHost;

/// <summary>
/// Where the plugins are, as the host gives them: never chosen by a user (ADR-0015, ADR-0020).
/// </summary>
/// <param name="ShippedDirectory">The folder of the plugins shipped with Voyager; null for none.</param>
/// <param name="UserDirectory">
/// The folder of the installed plugins and of the packages prepared to install them; null when plugins cannot be
/// installed, which is the case of the group server until the management of plugins comes to it (ADR-0020).
/// </param>
public sealed record PluginLocations(string? ShippedDirectory, string? UserDirectory)
{
    private const string INSTALLED_FOLDER = "installed";
    private const string STAGING_FOLDER = "staging";

    /// <summary>The folder of the installed plugins, one sub-folder each; null when plugins cannot be installed.</summary>
    public string? InstalledDirectory => UserDirectory is null ? null : Path.Combine(UserDirectory, INSTALLED_FOLDER);

    /// <summary>The private copies of the packages to install; null when plugins cannot be installed.</summary>
    public string? StagingDirectory => UserDirectory is null ? null : Path.Combine(UserDirectory, STAGING_FOLDER);

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
