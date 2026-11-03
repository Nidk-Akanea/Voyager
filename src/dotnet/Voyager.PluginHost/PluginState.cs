namespace Voyager.PluginHost;

/// <summary>
/// What became of one plugin folder at startup: loaded, set aside with the reason, or disabled by the user.
/// A plugin that fails later, while answering a call, is not tracked here: that failure belongs to the call.
/// </summary>
/// <param name="Folder">The name of the plugin's folder, unique within its origin even when two manifests share an identifier.</param>
/// <param name="Origin">Where the plugin comes from: shipped with Voyager, or installed by the user.</param>
/// <param name="Id">The identifier of the plugin; the folder name when the manifest could not be read.</param>
/// <param name="Version">The version of the plugin; empty when the manifest could not be read.</param>
/// <param name="Status">Whether the plugin is in use.</param>
/// <param name="RejectionReason">Why the plugin was set aside; null when it is loaded or disabled.</param>
/// <param name="UiModule">The user interface module of a loaded plugin, relative to its <c>ui</c> folder; null when it has none.</param>
/// <param name="Provides">The extension points a loaded plugin provides.</param>
/// <param name="Contracts">The contracts the plugin provides and requires, with their state; empty when the manifest could not be read.</param>
/// <param name="HasDotnet">Whether the plugin brings .NET code, as its manifest says.</param>
/// <param name="HasUi">Whether the plugin brings an interface part, as its manifest says.</param>
public sealed record PluginState(
    string Folder,
    PluginOrigin Origin,
    string Id,
    string Version,
    PluginStatus Status,
    PluginRejectionReason? RejectionReason,
    string? UiModule,
    IReadOnlyList<string> Provides,
    IReadOnlyList<ContractUse> Contracts,
    bool HasDotnet,
    bool HasUi)
{
    /// <summary>Who vouches for the plugin: the application for a shipped one, nobody yet for an installed one.</summary>
    public PluginVerification Verification =>
        Origin == PluginOrigin.Shipped ? PluginVerification.GuaranteedByApplication : PluginVerification.Unverified;
}
