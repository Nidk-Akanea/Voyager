namespace Voyager.PluginHost;

/// <summary>
/// Why a plugin was set aside at startup. The interface turns each reason into a sentence in the
/// user's language; the host never carries text.
/// </summary>
public enum PluginRejectionReason
{
    /// <summary>The manifest is missing, unreadable, incomplete, or does not match the plugin's folder.</summary>
    ManifestInvalid,

    /// <summary>Another plugin, found first, already has this identifier.</summary>
    DuplicateId,

    /// <summary>The plugin was written for a version of the SDK that this application does not accept.</summary>
    SdkIncompatible,

    /// <summary>The .NET part could not be loaded or configured.</summary>
    LoadFailed,

    /// <summary>The plugin registered an extension point that its manifest does not announce.</summary>
    UndeclaredRegistration,
}
