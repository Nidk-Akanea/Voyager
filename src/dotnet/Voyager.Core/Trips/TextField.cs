using Voyager.Core.Shared;

namespace Voyager.Core.Trips;

/// <summary>
/// The rules the texts of a traveler and of a trip have in common: cleaned, then not empty when required, and not too long.
/// </summary>
internal static class TextField
{
    #region Fields

    // A text is counted in characters as its reader sees them, and a single one may be made of many code units (a letter
    // with thousands of combining marks): past this many units for each character allowed, the text is too long all the
    // same, so that no text weighs more than a few kilobytes.
    private const int MAXIMUM_UNITS_PER_CHARACTER = 16;

    #endregion Fields

    #region Public Methods

    /// <summary>
    /// Checks a text.
    /// </summary>
    /// <param name="text">What the user typed.</param>
    /// <param name="field">The field, for the error.</param>
    /// <param name="maximumLength">The most characters allowed.</param>
    /// <param name="required">Whether the text must not be empty.</param>
    /// <param name="keepLineBreaks">Whether line breaks stay.</param>
    /// <returns>The cleaned text, or null when it is not required and nothing is left; else the reason it is refused.</returns>
    public static Result<string?> Validate(string? text, string field, int maximumLength, bool required, bool keepLineBreaks = false)
    {
        var cleaned = CleanText.Clean(text, keepLineBreaks);
        if (cleaned.Length == 0)
        {
            return required
                ? Result.Failure<string?>(TripsErrors.Invalid(field, TripsErrors.REQUIRED))
                : Result.Success<string?>(null);
        }

        return cleaned.Length > maximumLength * MAXIMUM_UNITS_PER_CHARACTER || CleanText.Length(cleaned) > maximumLength
            ? Result.Failure<string?>(TripsErrors.Invalid(field, TripsErrors.TOO_LONG))
            : Result.Success<string?>(cleaned);
    }

    #endregion Public Methods
}
