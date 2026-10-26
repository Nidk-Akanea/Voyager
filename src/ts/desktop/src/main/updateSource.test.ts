import { describe, expect, it } from 'vitest';

import { chooseUpdateSource, hideAddress, isAllowedUpdateUrl } from './updateSource';

describe('isAllowedUpdateUrl', () => {
  it.each([
    'https://updates.example.test/voyager/',
    'http://localhost:8080/',
    'http://127.0.0.1/voyager',
    'http://[::1]:9000/',
  ])('allows %s', (address) => {
    expect(isAllowedUpdateUrl(address)).toBe(true);
  });

  it.each([
    ['plain http to another machine', 'http://updates.example.test/voyager/'],
    ['plain http to a private address', 'http://192.0.2.10/'],
    ['another scheme', 'ftp://updates.example.test/'],
    ['a local file', 'file:///C:/updates/'],
    ['no scheme', 'updates.example.test/voyager'],
    ['nothing', ''],
    ['a password in the address', 'https://user:secret@updates.example.test/voyager/'],
    ['a user name in the address', 'https://user@updates.example.test/voyager/'],
    ['a token in the parameters', 'https://updates.example.test/voyager/?token=abc'],
    ['a fragment', 'https://updates.example.test/voyager/#abc'],
  ])('refuses %s', (_description, address) => {
    expect(isAllowedUpdateUrl(address)).toBe(false);
  });
});

describe('chooseUpdateSource', () => {
  it('uses the releases of the repository when nothing is configured', () => {
    expect(chooseUpdateSource(undefined)).toEqual({ kind: 'repository' });
    expect(chooseUpdateSource('')).toEqual({ kind: 'repository' });
  });

  it('uses the server a package was built for', () => {
    expect(chooseUpdateSource('https://updates.example.test/voyager/')).toEqual({
      kind: 'http',
      url: 'https://updates.example.test/voyager/',
    });
  });

  it('refuses an unsafe or unreadable setting, saying why', () => {
    expect(chooseUpdateSource('http://updates.example.test/').kind).toBe('invalid');
    expect(chooseUpdateSource(42)).toEqual({ kind: 'invalid', reason: 'voyagerUpdateSource is not text.' });
  });
});

describe('hideAddress', () => {
  const ADDRESS = 'https://updates.example.test/voyager/';

  it('hides the address, with or without its last slash, and the server alone', () => {
    const message =
      'GET https://updates.example.test/voyager/ failed; https://updates.example.test/voyager too; updates.example.test refused';

    expect(hideAddress(message, ADDRESS)).toBe(
      'GET <update server> failed; <update server> too; <update server> refused',
    );
  });

  it('leaves a message alone without an address', () => {
    expect(hideAddress('no route to host', undefined)).toBe('no route to host');
  });
});
