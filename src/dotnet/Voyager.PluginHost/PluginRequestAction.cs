namespace Voyager.PluginHost;

/// <summary>
/// What a request asks for a plugin. Requests are written by the main process of the desktop app after the user
/// confirmed them, and applied by the service at its next start, before any plugin loads (ADR-0016, decision 4).
/// </summary>
public enum PluginRequestAction
{
    /// <summary>Install, or replace, a plugin from a prepared package.</summary>
    Install,

    /// <summary>Remove an installed plugin and its files.</summary>
    Uninstall,

    /// <summary>Load the plugin again at startup.</summary>
    Enable,

    /// <summary>Stop loading the plugin at startup, without removing it.</summary>
    Disable,
}
