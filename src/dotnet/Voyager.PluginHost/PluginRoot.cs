namespace Voyager.PluginHost;

/// <summary>
/// A folder that holds plugins, one sub-folder each, and the origin of the plugins it holds.
/// </summary>
/// <param name="Path">The full path of the folder; a folder that does not exist holds no plugin.</param>
/// <param name="Origin">Where the plugins of this folder come from.</param>
public sealed record PluginRoot(string Path, PluginOrigin Origin);
