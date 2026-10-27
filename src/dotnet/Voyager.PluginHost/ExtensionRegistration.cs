namespace Voyager.PluginHost;

/// <summary>
/// One registration made by a plugin while it was configured, not yet checked against its manifest.
/// </summary>
/// <param name="ExtensionPoint">The extension point, from <see cref="Voyager.Sdk.ExtensionPoints"/>.</param>
/// <param name="ImplementationType">The type that implements it, resolved from the plugin's own container.</param>
internal sealed record ExtensionRegistration(string ExtensionPoint, Type ImplementationType);
