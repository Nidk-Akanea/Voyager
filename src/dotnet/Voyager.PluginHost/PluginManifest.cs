namespace Voyager.PluginHost;

/// <summary>
/// The content of the <c>plugin.json</c> file of a plugin: the only source of truth about what the
/// plugin is and what it provides (ADR-0015, ADR-0017). Created by <see cref="PluginManifestReader"/>, which
/// only returns manifests that passed validation.
/// </summary>
/// <param name="Id">Unique identifier: lower-case words separated by hyphens. It is also the plugin's folder name.</param>
/// <param name="Version">The version of the plugin, injected when the plugin is assembled.</param>
/// <param name="DotnetAssembly">File name of the .NET part, or null when the plugin has none.</param>
/// <param name="UiModule">Path of the user interface module under the plugin's <c>ui</c> folder, or null when it has none.</param>
/// <param name="Provides">The contracts the plugin provides, each with its version; each one is an extension point (<see cref="Voyager.Sdk.ExtensionPoints"/>).</param>
/// <param name="Requires">The contracts the plugin requires, each with its version; always includes the base contract <see cref="Voyager.Sdk.ContractVersions.PLUGIN"/>.</param>
public sealed record PluginManifest(
    string Id,
    string Version,
    string? DotnetAssembly,
    string? UiModule,
    IReadOnlyList<ContractReference> Provides,
    IReadOnlyList<ContractReference> Requires)
{
    /// <summary>
    /// Whether the plugin provides a contract, whatever its version.
    /// </summary>
    /// <param name="contract">The identifier of the contract, for example an extension point.</param>
    public bool ProvidesContract(string contract) =>
        Provides.Any(reference => string.Equals(reference.Contract, contract, StringComparison.Ordinal));
}
