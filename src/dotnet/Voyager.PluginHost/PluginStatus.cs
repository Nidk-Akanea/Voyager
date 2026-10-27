namespace Voyager.PluginHost;

/// <summary>
/// Whether a plugin found at startup is in use.
/// </summary>
public enum PluginStatus
{
    /// <summary>The plugin is loaded and what it provides is available.</summary>
    Loaded,

    /// <summary>The plugin was set aside; <see cref="PluginState.RejectionReason"/> says why.</summary>
    Rejected,
}
