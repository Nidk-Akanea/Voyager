namespace Voyager.PluginHost;

/// <summary>
/// A change the user confirmed for one plugin, waiting for the next start of the service.
/// </summary>
/// <param name="Id">The identifier of the request, unique among the pending ones.</param>
/// <param name="Action">What the request asks.</param>
/// <param name="Plugin">The plugin it concerns.</param>
/// <param name="Package">The package to install; null for any other request.</param>
public sealed record PluginRequest(string Id, PluginRequestAction Action, PluginKey Plugin, PluginPackageReference? Package = null);
