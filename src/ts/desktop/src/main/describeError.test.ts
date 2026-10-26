import { describe, expect, it } from 'vitest';

import { describeError } from './describeError';

describe('describeError', () => {
  it('gives the message of an error', () => {
    expect(describeError(new Error('The server did not answer.'))).toBe('The server did not answer.');
  });

  it('gives the text of anything else that was thrown', () => {
    expect(describeError('plain text')).toBe('plain text');
    expect(describeError(42)).toBe('42');
  });
});
