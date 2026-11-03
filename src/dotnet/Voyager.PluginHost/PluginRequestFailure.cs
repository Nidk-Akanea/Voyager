namespace Voyager.PluginHost;

/// <summary>
/// Why a request could not be applied. The interface turns each reason into a sentence in the user's language.
/// </summary>
public enum PluginRequestFailure
{
    /// <summary>The plugin the request names is not there.</summary>
    NotFound,

    /// <summary>The request does not apply to this plugin, for example uninstalling a plugin shipped with Voyager.</summary>
    NotAllowed,

    /// <summary>A folder or a file could not be changed: rights, a full disk, or a file locked by another program.</summary>
    FileSystemError,

    /// <summary>This version of the application does not apply such a request yet.</summary>
    NotSupported,
}
