using System.Reflection;

namespace Voyager.Sdk;

/// <summary>
/// Information about the version of the SDK that is running.
/// </summary>
public static class SdkInfo
{
    /// <summary>
    /// The version of this SDK, computed by GitVersion from the history of the SDK alone (<c>sdk/v*</c> tags,
    /// ADR-0007) and never written by hand. A plugin states the major and minor version it was written for
    /// in the <c>sdk</c> field of its manifest.
    /// </summary>
    public static Version Version { get; } = ReadVersion();

    // The file version, not the assembly version: the latter is the identity plugins bind to and never changes.
    // Version 0.0 (compatible with no plugin) when the assembly carries no readable file version.
    private static Version ReadVersion()
    {
        var fileVersion = typeof(SdkInfo).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        return Version.TryParse(fileVersion, out var version) ? version : new Version(0, 0);
    }
}
