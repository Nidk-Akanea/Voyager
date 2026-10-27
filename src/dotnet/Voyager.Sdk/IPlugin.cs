namespace Voyager.Sdk;

/// <summary>
/// The entry point of the .NET part of a plugin. A plugin assembly contains exactly one public,
/// non-abstract class that implements this interface and has a public parameterless constructor.
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Called once, when Voyager starts, to let the plugin declare what it provides. Register the
    /// services of the plugin and its extension points on <paramref name="builder"/>; nothing else of
    /// the host is reachable from here. Keep it quick and free of side effects outside the plugin: an
    /// exception thrown from here, or a registration that the manifest does not announce, sets the plugin
    /// aside and nothing it registered is kept.
    /// </summary>
    /// <param name="builder">Where the plugin registers its services and its extension points.</param>
    void Configure(IPluginBuilder builder);
}
