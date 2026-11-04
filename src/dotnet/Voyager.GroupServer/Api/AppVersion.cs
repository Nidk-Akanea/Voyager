namespace Voyager.GroupServer.Api;

/// <summary>
/// Reads the version of a desktop app, as the server compares it with the minimum it serves (ADR-0018, decision 6).
/// </summary>
internal static class AppVersion
{
    #region Public Methods

    /// <summary>
    /// Major, minor and patch of a version such as <c>0.4.0-beta.2</c> or <c>1.0.0+abc123</c>: the prerelease and
    /// build suffixes are ignored, so a prerelease counts as its final version.
    /// </summary>
    /// <param name="text">The version, as the app gives it or as the setting holds it.</param>
    /// <returns>The version, or null when the text is no version in the form x.y.z.</returns>
    public static Version? ParseCore(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var core = text.Trim();
        var suffix = core.IndexOfAny(['-', '+']);
        if (suffix >= 0)
        {
            core = core[..suffix];
        }

        return Version.TryParse(core, out var version) && version.Build >= 0 && version.Revision < 0 ? version : null;
    }

    #endregion Public Methods
}
