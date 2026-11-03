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

    /// <summary>
    /// A plugin shipped with Voyager already has this identifier: the user can disable it to use this installed one.
    /// </summary>
    DuplicateOfShipped,

    /// <summary>
    /// A contract that the plugin provides or requires is in a version that this application does not accept:
    /// obsolete or unknown (<see cref="PluginState.Contracts"/> says which).
    /// </summary>
    ContractNotSupported,

    /// <summary>The .NET part could not be loaded or configured.</summary>
    LoadFailed,

    /// <summary>The plugin registered an extension point that its manifest does not announce.</summary>
    UndeclaredRegistration,
}
