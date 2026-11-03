namespace Voyager.PluginHost.Packages;

/// <summary>
/// What the examination of a prepared package found, for the confirmation of its installation. Nothing is installed by
/// an examination. The texts that come from the package are identifiers and versions that the manifest reader checked.
/// </summary>
/// <param name="Sha256">The SHA-256 fingerprint of the package, in lower-case hexadecimal.</param>
/// <param name="Size">The size of the package, in bytes.</param>
/// <param name="Refusal">Why the package is refused; null when it can be installed.</param>
/// <param name="Manifest">The manifest of the plugin; null when it could not be read.</param>
/// <param name="Contracts">The contracts of the plugin with their state; empty when the manifest could not be read.</param>
/// <param name="DuplicateOfShipped">Whether a plugin shipped with Voyager already has this identifier.</param>
/// <param name="InstalledVersion">The version of the plugin installed under this identifier; null when there is none.</param>
/// <param name="Kind">What installing it would do; meaningful only when the manifest could be read.</param>
public sealed record PackageExamination(
    string Sha256,
    long Size,
    PackageRefusal? Refusal,
    PluginManifest? Manifest,
    IReadOnlyList<ContractUse> Contracts,
    bool DuplicateOfShipped,
    string? InstalledVersion,
    PackageInstallKind Kind)
{
    /// <summary>Who vouches for the plugin: nobody yet for a file, since no signature key is decided (question Q31).</summary>
    public static PluginVerification Verification => PluginVerification.Unverified;
}
