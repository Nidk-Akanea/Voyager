import { describe, expect, it } from 'vitest';

import {
  DEVELOPMENT_SERVER_ID,
  normalizeServerUrl,
  parseServerSettings,
  readRegisteredServers,
  ServerSettingsError,
} from './serverRegistry';

describe('normalizeServerUrl', () => {
  it.each([
    ['https://voyager.example.org', 'https://voyager.example.org'],
    ['https://voyager.example.org/', 'https://voyager.example.org'],
    ['https://example.org/voyager/', 'https://example.org/voyager'],
    ['http://localhost:5180', 'http://localhost:5180'],
    ['http://127.0.0.1:5180', 'http://127.0.0.1:5180'],
    ['http://[::1]:5180', 'http://[::1]:5180'],
  ])('accepts %s', (text, expected) => {
    expect(normalizeServerUrl(text)).toBe(expected);
  });

  it.each([
    ['plain HTTP on the network', 'http://voyager.example.org'],
    ['a user name and password', 'https://traveler-a:secret@voyager.example.org'],
    ['a query', 'https://voyager.example.org/?token=abc'],
    ['a fragment', 'https://voyager.example.org/#top'],
    ['another scheme', 'ftp://voyager.example.org'],
    ['no address at all', 'not an address'],
  ])('refuses %s', (_case, text) => {
    expect(normalizeServerUrl(text)).toBeUndefined();
  });
});

describe('parseServerSettings', () => {
  it('reads the servers of the file, in their order', () => {
    const servers = parseServerSettings(
      JSON.stringify({
        servers: [
          { id: 'family', url: 'https://voyager.example.org/' },
          { id: 'friends-2027', url: 'https://friends.example.org' },
        ],
      }),
    );

    expect(servers).toEqual([
      { id: 'family', url: 'https://voyager.example.org' },
      { id: 'friends-2027', url: 'https://friends.example.org' },
    ]);
  });

  it.each([
    ['text that is not JSON', '{ servers: '],
    ['no list of servers', '{}'],
    ['an identifier in capitals', '{ "servers": [{ "id": "Family", "url": "https://voyager.example.org" }] }'],
    [
      'a server named twice',
      '{ "servers": [{ "id": "a", "url": "https://a.example.org" }, { "id": "a", "url": "https://b.example.org" }] }',
    ],
    ['a refused address', '{ "servers": [{ "id": "family", "url": "http://voyager.example.org" }] }'],
  ])('refuses %s', (_case, text) => {
    expect(() => parseServerSettings(text)).toThrow(ServerSettingsError);
  });

  it('never quotes the address of a refused server', () => {
    const text = '{ "servers": [{ "id": "family", "url": "http://private-host.example.org" }] }';

    expect(() => parseServerSettings(text)).toThrow(/^(?!.*private-host).*$/);
  });
});

describe('readRegisteredServers', () => {
  it('uses the development server when Aspire gives one, whatever the file says', () => {
    const servers = readRegisteredServers({
      developmentUrl: 'http://localhost:5180',
      readSettingsFile: () => '{ "servers": [{ "id": "family", "url": "https://voyager.example.org" }] }',
    });

    expect(servers).toEqual([{ id: DEVELOPMENT_SERVER_ID, url: 'http://localhost:5180' }]);
  });

  it('knows no server without a settings file', () => {
    expect(readRegisteredServers({ readSettingsFile: () => undefined })).toEqual([]);
  });

  it('reads the settings file of the installed app', () => {
    const servers = readRegisteredServers({
      readSettingsFile: () => '{ "servers": [{ "id": "family", "url": "https://voyager.example.org" }] }',
    });

    expect(servers.map((server) => server.id)).toEqual(['family']);
  });
});
