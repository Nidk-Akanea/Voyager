// Cleans a text that comes from a package or from the user's disk before a dialog shows it: an author or a file name
// could otherwise break a line, hide part of a sentence or reverse it (spec of the plugin management, rule 6). The
// same rules as DisplayText.Clean in src/dotnet/Voyager.PluginHost/DisplayText.cs.

/** The longest text shown, in UTF-16 code units, ellipsis included: the length .NET counts. */
export const MAXIMUM_DISPLAY_LENGTH = 100;

const ELLIPSIS = '…';
// Control and format characters (those that change the direction of the text, the invisible ones), and the two
// separators that break a line as surely as a line feed.
const HIDDEN_CHARACTERS = /[\p{Cc}\p{Cf}\p{Zl}\p{Zp}]/gu;
// A half of a character with no other half: it shows as a question mark in a box.
const LONE_SURROGATES = /\p{Cs}/gu;

function isHighSurrogate(code: number): boolean {
  return code >= 0xd800 && code <= 0xdbff;
}

/** The text without control characters (line breaks included) nor bidirectional formatting, cut with an ellipsis. */
export function cleanDisplayText(text: string): string {
  const cleaned = text.replace(HIDDEN_CHARACTERS, '').replace(LONE_SURROGATES, '').trim();
  if (cleaned.length <= MAXIMUM_DISPLAY_LENGTH) {
    return cleaned;
  }
  // Never cut between the two halves of a character outside the basic plane, such as an emoji.
  let length = MAXIMUM_DISPLAY_LENGTH - 1;
  if (isHighSurrogate(cleaned.charCodeAt(length - 1))) {
    length--;
  }
  return `${cleaned.slice(0, length).trimEnd()}${ELLIPSIS}`;
}
