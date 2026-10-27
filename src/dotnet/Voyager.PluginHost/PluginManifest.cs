namespace Voyager.PluginHost;

/// <summary>
/// The content of the <c>plugin.json</c> file of a plugin: the only source of truth about what the
/// plugin is and what it provides (ADR-0015). Created by <see cref="PluginManifestReader"/>, which
/// only returns manifests that passed validation.
/// </summary>
/// <param name="Id">Unique identifier: lower-case words separated by hyphens. It is also the plugin's folder name.</param>
/// <param name="Version">The version of the plugin, injected when the plugin is assembled.</param>
/// <param name="Sdk">The major and minor version of the SDK the plugin was written for, for example <c>0.1</c>.</param>
/// <param name="DotnetAssembly">File name of the .NET part, or null when the plugin has none.</param>
/// <param name="UiModule">Path of the user interface module under the plugin's <c>ui</c> folder, or null when it has none.</param>
/// <param name="Provides">The extension points the plugin provides (<see cref="Voyager.Sdk.ExtensionPoints"/>).</param>
public sealed record PluginManifest(
    string Id,
    string Version,
    string Sdk,
    string? DotnetAssembly,
    string? UiModule,
    IReadOnlyList<string> Provides);
