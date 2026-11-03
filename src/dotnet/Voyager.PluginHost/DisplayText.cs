using System.Globalization;
using System.Text;

namespace Voyager.PluginHost;

/// <summary>
/// Cleans a text that comes from a package or from the user's disk before it is shown: an author or a file name
/// could otherwise break a line, hide part of a sentence or reverse it (spec of the plugin management, rule 6).
/// </summary>
public static class DisplayText
{
    #region Fields

    /// <summary>The longest text shown, in characters, ellipsis included.</summary>
    public const int MAXIMUM_LENGTH = 100;

    private const char ELLIPSIS = '…';

    #endregion Fields

    #region Private Methods

    // A half of a character with no other half is not a character: it shows as a question mark in a box.
    private static string WithoutLoneSurrogates(string text)
    {
        var result = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                result.Append(character).Append(text[index + 1]);
                index++;
            }
            else if (!char.IsSurrogate(character))
            {
                result.Append(character);
            }
        }

        return result.ToString();
    }

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// The text without control characters (line breaks included) nor bidirectional formatting, and cut to
    /// <see cref="MAXIMUM_LENGTH"/> characters with an ellipsis.
    /// </summary>
    /// <param name="text">The text to clean.</param>
    public static string Clean(string text)
    {
        var cleaned = new StringBuilder(Math.Min(text.Length, MAXIMUM_LENGTH));
        foreach (var character in text)
        {
            // Format characters include those that change the direction of the text around them, and the invisible
            // ones; the two separators break a line as surely as a line feed.
            if (char.GetUnicodeCategory(character) is UnicodeCategory.Control or UnicodeCategory.Format
                or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
            {
                continue;
            }

            cleaned.Append(character);
        }

        var result = WithoutLoneSurrogates(cleaned.ToString()).Trim();
        if (result.Length <= MAXIMUM_LENGTH)
        {
            return result;
        }

        // Never cut between the two halves of a character outside the basic plane, such as an emoji.
        var length = MAXIMUM_LENGTH - 1;
        if (char.IsHighSurrogate(result[length - 1]))
        {
            length--;
        }

        return string.Concat(result.AsSpan(0, length).TrimEnd(), ELLIPSIS.ToString());
    }

    #endregion Public Methods
}
