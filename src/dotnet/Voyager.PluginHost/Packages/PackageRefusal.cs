namespace Voyager.PluginHost.Packages;

/// <summary>
/// Why a package of plugin is refused at its examination (spec of the plugin management, "Installer depuis un
/// fichier", rule 5). The interface turns each reason into a sentence in the user's language.
/// </summary>
public enum PackageRefusal
{
    /// <summary>The file is not a readable archive.</summary>
    Unreadable,

    /// <summary>The manifest is missing, invalid, declares no part, or names a reserved identifier.</summary>
    ManifestInvalid,

    /// <summary>A file the manifest declares (the assembly, the interface module) is not in the package.</summary>
    DeclaredFileMissing,

    /// <summary>A contract the plugin provides or requires is in a version the application does not support.</summary>
    ContractNotSupported,

    /// <summary>The package holds a file or a name that is not allowed: outside the plugin, a link, a native library…</summary>
    ForbiddenEntry,

    /// <summary>The disk does not have the space to extract the package.</summary>
    NotEnoughSpace,
}
