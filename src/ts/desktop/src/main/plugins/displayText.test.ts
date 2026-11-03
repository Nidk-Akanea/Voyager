import { describe, expect, it } from 'vitest';

import { cleanDisplayText, MAXIMUM_DISPLAY_LENGTH } from './displayText';

describe('cleanDisplayText', () => {
  it('keeps a plain text as it is', () => {
    expect(cleanDisplayText('diagnostics-clock-1.2.0.voyagerplugin')).toBe('diagnostics-clock-1.2.0.voyagerplugin');
  });

  it('removes line breaks, tabs and the Unicode separators of lines and paragraphs', () => {
    expect(cleanDisplayText('first\nsecond\r\tthird fourth fifth')).toBe('firstsecondthirdfourthfifth');
  });

  it('removes the characters that reverse or hide the text around them', () => {
    // A right-to-left override would show "exe.txt" as "txt.exe".
    expect(cleanDisplayText('report‮txt.exe​⁦')).toBe('reporttxt.exe');
  });

  it('removes a half of a character with no other half, and keeps an emoji whole', () => {
    expect(cleanDisplayText('a\uD800b🙂')).toBe('ab🙂');
  });

  it('trims the spaces left at both ends', () => {
    expect(cleanDisplayText('  \u0007 name  ')).toBe('name');
  });

  it('cuts a long text with an ellipsis, at the maximum length', () => {
    const cleaned = cleanDisplayText('x'.repeat(250));

    expect(cleaned).toHaveLength(MAXIMUM_DISPLAY_LENGTH);
    expect(cleaned.endsWith('…')).toBe(true);
  });

  it('never cuts an emoji in two', () => {
    // The 99th code unit is the first half of an emoji: the cut comes before it.
    const cleaned = cleanDisplayText(`${'x'.repeat(98)}🙂${'y'.repeat(10)}`);

    expect(cleaned).toBe(`${'x'.repeat(98)}…`);
  });

  it('keeps a text of exactly the maximum length', () => {
    expect(cleanDisplayText('x'.repeat(MAXIMUM_DISPLAY_LENGTH))).toBe('x'.repeat(MAXIMUM_DISPLAY_LENGTH));
  });
});
