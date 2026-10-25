import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

import { resolveUserInterfaceFile } from './userInterfaceFiles';

const DIRECTORY = join('voyager', 'ui');

describe('resolveUserInterfaceFile', () => {
  it('serves the entry page for the root', () => {
    expect(resolveUserInterfaceFile(DIRECTORY, '/')).toBe(join(DIRECTORY, 'index.html'));
  });

  it('serves a file of the user interface', () => {
    expect(resolveUserInterfaceFile(DIRECTORY, '/assets/index.js')).toBe(join(DIRECTORY, 'assets', 'index.js'));
  });

  it('keeps a name that merely starts with two dots', () => {
    expect(resolveUserInterfaceFile(DIRECTORY, '/..icons.svg')).toBe(join(DIRECTORY, '..icons.svg'));
  });

  it.each([
    ['a parent folder', '/../secret.txt'],
    ['an encoded parent folder', '/%2e%2e/secret.txt'],
    ['an encoded backslash', '/..%5csecret.txt'],
    ['an absolute path', '/%2fetc%2fpasswd/../../../../etc/passwd'],
    ['the folder itself', '/assets/..'],
    ['a malformed escape', '/%E0%A4%A'],
  ])('refuses %s', (_case, urlPath) => {
    expect(resolveUserInterfaceFile(DIRECTORY, urlPath)).toBeUndefined();
  });
});
