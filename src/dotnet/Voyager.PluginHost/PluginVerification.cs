namespace Voyager.PluginHost;

/// <summary>
/// Who vouches for a plugin (ADR-0016, decision 7). No signature key is decided yet (question Q31): only what ships
/// with the application is vouched for.
/// </summary>
public enum PluginVerification
{
    /// <summary>Shipped with Voyager: the signature of the application covers it.</summary>
    GuaranteedByApplication,

    /// <summary>Installed from a file that no known key signs: code of a third party, accepted at the user's risk.</summary>
    Unverified,
}
