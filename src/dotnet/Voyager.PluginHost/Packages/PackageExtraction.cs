namespace Voyager.PluginHost.Packages;

/// <summary>
/// What the extraction of a package found.
/// </summary>
/// <param name="Sha256">The SHA-256 fingerprint of the bytes that were read, in lower-case hexadecimal.</param>
/// <param name="Size">The size of the package, in bytes.</param>
/// <param name="Manifest">The manifest of the plugin; null when it could not be read.</param>
/// <param name="Contracts">The contracts of the plugin with their state; empty when the manifest could not be read.</param>
/// <param name="Refusal">Why the package is refused; null when it is accepted.</param>
internal sealed record PackageExtraction(
    string Sha256,
    long Size,
    PluginManifest? Manifest,
    IReadOnlyList<ContractUse> Contracts,
    PackageRefusal? Refusal);
