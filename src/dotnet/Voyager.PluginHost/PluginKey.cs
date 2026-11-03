namespace Voyager.PluginHost;

/// <summary>
/// What tells two plugins apart: a shipped plugin and an installed one may share an identifier.
/// </summary>
/// <param name="Origin">Where the plugin comes from.</param>
/// <param name="Id">The identifier of the plugin, which is also the name of its folder.</param>
public sealed record PluginKey(PluginOrigin Origin, string Id);
