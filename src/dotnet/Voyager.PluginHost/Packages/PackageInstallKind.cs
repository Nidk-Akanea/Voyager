namespace Voyager.PluginHost.Packages;

/// <summary>
/// What installing a package would do, compared with the plugin already installed under the same identifier (spec of
/// the plugin management, "Installer depuis un fichier", rule 9).
/// </summary>
public enum PackageInstallKind
{
    /// <summary>No plugin of this identifier is installed.</summary>
    New,

    /// <summary>It replaces an older version.</summary>
    Upgrade,

    /// <summary>It replaces a newer version: an operation of its own, which the confirmation warns of.</summary>
    Downgrade,

    /// <summary>It installs the same version again.</summary>
    Reinstall,
}
