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

    /// <summary>A folder or a file could not be changed: rights, or a file locked by another program.</summary>
    FileSystemError,

    /// <summary>This version of the application does not apply such a request yet.</summary>
    NotSupported,

    /// <summary>The prepared copy of the package is no longer there.</summary>
    PackageMissing,

    /// <summary>The prepared copy changed since the user confirmed it: its fingerprint is not the one they saw.</summary>
    PackageChanged,

    /// <summary>The examination refused the package, or it holds another plugin than the request names.</summary>
    PackageRefused,

    /// <summary>The disk does not have the space to install the plugin.</summary>
    NotEnoughSpace,
}
