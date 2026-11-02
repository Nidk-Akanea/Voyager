namespace Voyager.PluginHost;

/// <summary>
/// Whether this application accepts the version of a contract that a plugin uses.
/// </summary>
public enum ContractUseState
{
    /// <summary>The application accepts this version.</summary>
    Supported,

    /// <summary>The application accepts this version, and a later release will remove it.</summary>
    Deprecated,

    /// <summary>The application no longer accepts this version: the plugin is too old.</summary>
    Obsolete,

    /// <summary>
    /// The application does not know this contract or this version: the plugin is too recent, or requires a
    /// contract that the application does not provide.
    /// </summary>
    Unknown,
}
