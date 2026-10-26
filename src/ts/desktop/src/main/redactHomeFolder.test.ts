import { describe, expect, it } from 'vitest';

import { redactHomeFolder, redactLogData } from './redactHomeFolder';

const HOME = String.raw`C:\Users\someone`;

describe('redactHomeFolder', () => {
  it('hides the home folder in a Windows path', () => {
    const text = String.raw`spawn C:\Users\someone\AppData\Local\Voyager\Voyager.LocalService.exe ENOENT`;

    expect(redactHomeFolder(text, HOME)).toBe(
      String.raw`spawn ~\AppData\Local\Voyager\Voyager.LocalService.exe ENOENT`,
    );
  });

  it('hides it with slashes, whatever the case, and every time it appears', () => {
    const text = 'c:/users/Someone/a.db and C:/Users/someone/b.db';

    expect(redactHomeFolder(text, HOME)).toBe('~/a.db and ~/b.db');
  });

  it('leaves other text and other folders alone', () => {
    const text = String.raw`started at C:\Program Files\Voyager`;

    expect(redactHomeFolder(text, HOME)).toBe(text);
  });

  it('does nothing without a usable home folder', () => {
    expect(redactHomeFolder('/var/log/a.log', '/')).toBe('/var/log/a.log');
    expect(redactHomeFolder('a.log', '')).toBe('a.log');
  });

  it('treats the folder as text, not as a pattern', () => {
    expect(redactHomeFolder('C:\\Users\\a(b)\\x and C:\\Users\\aab\\x', String.raw`C:\Users\a(b)`)).toBe(
      '~\\x and C:\\Users\\aab\\x',
    );
  });
});

describe('redactHomeFolder boundaries', () => {
  it('does not cut the name of another folder that starts the same way', () => {
    const text = String.raw`C:\Users\someone and C:\Users\someoneelse\a.db`;

    expect(redactHomeFolder(text, HOME)).toBe(String.raw`~ and C:\Users\someoneelse\a.db`);
  });
});

describe('redactLogData', () => {
  it('hides the home folder in text and in the message and stack of an error', () => {
    const error = new Error(String.raw`cannot open C:\Users\someone\a.db`);
    error.stack = String.raw`Error: cannot open C:\Users\someone\a.db
    at start (C:\Users\someone\AppData\Local\Voyager\app.asar\main.js:1:1)`;

    const [text, redactedError] = redactLogData([String.raw`in C:\Users\someone`, error], HOME);

    expect(text).toBe('in ~');
    expect(redactedError).toBe(String.raw`Error: cannot open ~\a.db
    at start (~\AppData\Local\Voyager\app.asar\main.js:1:1)`);
  });

  it('keeps what is not text as it is', () => {
    const data = { count: 3 };

    expect(redactLogData([data, 7], HOME)).toEqual([data, 7]);
  });
});
