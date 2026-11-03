namespace Voyager.PluginHost.Packages;

/// <summary>
/// Compares two versions of a plugin, as Semantic Versioning orders them: <c>major.minor.patch</c>, then a version
/// without a pre-release label before the same one with it, then the labels part by part. Build metadata (<c>+…</c>)
/// does not count. The manifest reader has already checked the format.
/// </summary>
internal static class PluginVersions
{
    #region Private Methods

    private static (string[] Numbers, string[] Labels) Parse(string version)
    {
        var withoutBuild = version.Split('+')[0];
        var dash = withoutBuild.IndexOf('-', StringComparison.Ordinal);
        var core = dash < 0 ? withoutBuild : withoutBuild[..dash];
        var labels = dash < 0 ? [] : withoutBuild[(dash + 1)..].Split('.');
        return (core.Split('.'), labels);
    }

    private static bool IsNumber(string text) => text.Length > 0 && text.All(char.IsAsciiDigit);

    // Numbers of any length, as the manifest allows them: compared by their digits, never read into an integer type
    // that a long one would overflow.
    private static int CompareNumbers(string left, string right)
    {
        var leftDigits = left.TrimStart('0');
        var rightDigits = right.TrimStart('0');
        return leftDigits.Length != rightDigits.Length
            ? leftDigits.Length.CompareTo(rightDigits.Length)
            : string.CompareOrdinal(leftDigits, rightDigits);
    }

    private static int CompareLabel(string left, string right)
    {
        return (IsNumber(left), IsNumber(right)) switch
        {
            (true, true) => CompareNumbers(left, right),
            // Numeric identifiers come before alphanumeric ones.
            (true, false) => -1,
            (false, true) => 1,
            _ => string.CompareOrdinal(left, right),
        };
    }

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// Compares two versions.
    /// </summary>
    /// <returns>Less than zero when <paramref name="left"/> is older, zero when they are the same, more than zero when it is newer.</returns>
    public static int Compare(string left, string right)
    {
        var (leftNumbers, leftLabels) = Parse(left);
        var (rightNumbers, rightLabels) = Parse(right);
        for (var index = 0; index < Math.Max(leftNumbers.Length, rightNumbers.Length); index++)
        {
            var comparison = CompareNumbers(leftNumbers.ElementAtOrDefault(index) ?? "0", rightNumbers.ElementAtOrDefault(index) ?? "0");
            if (comparison != 0)
            {
                return comparison;
            }
        }

        // A pre-release comes before the release it prepares.
        switch (leftLabels.Length, rightLabels.Length)
        {
            case (0, 0):
                return 0;
            case (0, _):
                return 1;
            case (_, 0):
                return -1;
        }

        for (var index = 0; index < Math.Min(leftLabels.Length, rightLabels.Length); index++)
        {
            var comparison = CompareLabel(leftLabels[index], rightLabels[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return leftLabels.Length.CompareTo(rightLabels.Length);
    }

    #endregion Public Methods
}
