using System.Reflection;

namespace Voyager.Sdk;

/// <summary>
/// Information about the version of the SDK that is running.
/// </summary>
public static class SdkInfo
{
    #region Properties

    /// <summary>
    /// The version of this SDK, computed by GitVersion from the history of the SDK alone (<c>sdk/v*</c> tags,
    /// ADR-0007) and never written by hand. It tells what changed in the SDK, not which plugins it accepts:
    /// a plugin is judged contract by contract (<see cref="ContractVersions"/>, ADR-0017).
    /// </summary>
    public static Version Version { get; } = ReadVersion();

    #endregion Properties

    #region Private Methods

    // The file version, not the assembly version: the latter is the identity plugins bind to and never changes.
    // Version 0.0 when the assembly carries no readable file version.
    private static Version ReadVersion()
    {
        var fileVersion = typeof(SdkInfo).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        return Version.TryParse(fileVersion, out var version) ? version : new Version(0, 0);
    }

    #endregion Private Methods
}
