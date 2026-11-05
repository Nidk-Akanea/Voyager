import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

import { findServedPlugin, servedPluginKey } from './servedPlugins';

describe('findServedPlugin', () => {
  const folder = join('servers', 'family', 'plugins', 'diagnostics-clock', '1.2.0');
  const find = findServedPlugin(new Map([[servedPluginKey('shipped', 'diagnostics-clock', '1.2.0'), folder]]));

  it('finds the folder of the version the server loaded', () => {
    expect(find('shipped', 'diagnostics-clock', '1.2.0')).toBe(folder);
  });

  it.each([
    ['another version', 'shipped', 'diagnostics-clock', '1.1.0'],
    ['another origin', 'installed', 'diagnostics-clock', '1.2.0'],
    ['another plugin', 'shipped', 'diagnostics-runtime', '1.2.0'],
  ] as const)('finds nothing for %s', (_case, origin, pluginId, version) => {
    expect(find(origin, pluginId, version)).toBeUndefined();
  });
});
