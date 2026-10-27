namespace Voyager.PluginHost;

/// <summary>
/// What became of one plugin folder at startup: loaded, or set aside with the reason. Shown on the
/// "About" page. A plugin that fails later, while answering a call, is not tracked here: that failure
/// belongs to the call.
/// </summary>
/// <param name="Folder">The name of the plugin's folder, which is unique even when two manifests share an identifier.</param>
/// <param name="Id">The identifier of the plugin; the folder name when the manifest could not be read.</param>
/// <param name="Version">The version of the plugin; empty when the manifest could not be read.</param>
/// <param name="Status">Whether the plugin is in use.</param>
/// <param name="RejectionReason">Why the plugin was set aside; null when it is loaded.</param>
/// <param name="UiModule">The user interface module of a loaded plugin, relative to its <c>ui</c> folder; null when it has none.</param>
/// <param name="Provides">The extension points a loaded plugin provides.</param>
public sealed record PluginState(
    string Folder,
    string Id,
    string Version,
    PluginStatus Status,
    PluginRejectionReason? RejectionReason,
    string? UiModule,
    IReadOnlyList<string> Provides);
