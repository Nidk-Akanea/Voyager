namespace Voyager.PluginHost;

/// <summary>
/// Where a plugin comes from (ADR-0016, decision 1): each origin has its own folder, read in this order.
/// </summary>
public enum PluginOrigin
{
    /// <summary>Shipped with Voyager, in the application's folder: it can be disabled, never uninstalled.</summary>
    Shipped,

    /// <summary>Installed by the user from a package, in the user's data folder: it stays across updates.</summary>
    Installed,
}
