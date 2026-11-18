using System.Globalization;
using System.Text;

namespace Voyager.Core.Shared;

/// <summary>
/// What every text typed by a user goes through before it is kept: no space around it, no control character, no
/// invisible character, and nothing that changes the direction of the writing, which could make a name read differently
/// from how it is written or look empty.
/// </summary>
internal static class CleanText
{
    #region Public Methods

    /// <summary>
    /// Cleans a text.
    /// </summary>
    /// <param name="text">What the user typed; null counts as nothing.</param>
    /// <param name="keepLineBreaks">Whether a line break stays (a description) or goes with the other control characters.</param>
    /// <returns>The cleaned text, empty when nothing is left.</returns>
    public static string Clean(string? text, bool keepLineBreaks = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                // A character outside the basic plane, whole: its two halves stay together.
                builder.Append(character).Append(text[index + 1]);
                index++;
                continue;
            }

            // A half alone is not text: it could not be written to the storage nor sent on the network.
            if (char.IsSurrogate(character) || IsInvisible(character))
            {
                continue;
            }

            if (char.IsControl(character))
            {
                // A carriage return always goes: a "\r\n" becomes the single "\n" that stays.
                if (keepLineBreaks && character == '\n')
                {
                    builder.Append('\n');
                }

                continue;
            }

            builder.Append(character);
        }

        return builder.ToString().Trim();
    }

    /// <summary>
    /// The length of a text as its reader counts it: a letter with its accent, or an emoji, is one character.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>How many characters it has.</returns>
    public static int Length(string text) => new StringInfo(text).LengthInTextElements;

    #endregion Public Methods

    #region Private Methods

    // The formatting characters (a zero-width space, a soft hyphen, the marks and overrides of the Unicode bidirectional
    // algorithm...), and the line and paragraph separators, which would break a line in a name. Two formatting characters
    // stay: the joiner of the emoji sequences and the non-joiner that some scripts need to be written right.
    private static bool IsInvisible(char character) =>
        (char.GetUnicodeCategory(character) == UnicodeCategory.Format && character is not ('\u200C' or '\u200D'))
        || character is '\u2028' or '\u2029';

    #endregion Private Methods
}
