using System.Globalization;

namespace Voyager.PluginHost;

/// <summary>
/// The rule that tells whether the SDK that runs accepts a plugin written for another SDK version
/// (ADR-0015, decision 8): a few lines instead of a version-range library.
/// </summary>
public static class SdkCompatibility
{
    #region Public Methods

    /// <summary>
    /// A plugin written for SDK <c>M.m</c> is accepted by an SDK of the same major version and a minor
    /// version greater than or equal to <c>m</c>. While the major version is 0, any minor version may
    /// break the contract: the minor versions must be equal.
    /// </summary>
    /// <param name="requested">The <c>sdk</c> field of the manifest, in the <c>major.minor</c> format.</param>
    /// <param name="running">The version of the SDK that runs.</param>
    /// <returns>True when the plugin is accepted.</returns>
    public static bool IsAccepted(string requested, Version running)
    {
        var parts = requested.Split('.');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor))
        {
            return false;
        }

        if (major != running.Major)
        {
            return false;
        }

        return major == 0 ? minor == running.Minor : minor <= running.Minor;
    }

    #endregion Public Methods
}
