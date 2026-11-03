namespace Voyager.PluginHost;

/// <summary>
/// The package an installation request names: the private copy the desktop app made of it, and what the user saw
/// when they confirmed (ADR-0016, decision 3).
/// </summary>
/// <param name="StagedName">The name of the copy in the staging folder, given by the desktop app; never a path.</param>
/// <param name="Sha256">The SHA-256 fingerprint the user confirmed, in lower-case hexadecimal: checked again before installing.</param>
/// <param name="Name">The name of the file the user chose, kept as the origin of the plugin; shown cleaned, never logged.</param>
public sealed record PluginPackageReference(string StagedName, string Sha256, string Name);
