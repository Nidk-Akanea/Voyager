import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

import { PluginOrigin, PluginStatus } from '@voyager/grpc-client';

import { findServedPlugin, servedPluginFolders } from './servedPlugins';

const ROOTS = { shipped: join('app', 'plugins'), installed: join('data', 'plugins', 'installed') };

function plugin(id: string, origin: PluginOrigin, status = PluginStatus.LOADED, folder = id) {
  return { id, folder, origin, status };
}

describe('servedPluginFolders', () => {
  it('serves each loaded plugin from the folder of its origin', () => {
    const find = findServedPlugin(
      servedPluginFolders(
        [plugin('diagnostics-clock', PluginOrigin.SHIPPED), plugin('itinerary-map', PluginOrigin.INSTALLED)],
        ROOTS,
      ),
    );

    expect(find('shipped', 'diagnostics-clock')).toBe(join(ROOTS.shipped, 'diagnostics-clock'));
    expect(find('installed', 'itinerary-map')).toBe(join(ROOTS.installed, 'itinerary-map'));
    expect(find('installed', 'diagnostics-clock')).toBeUndefined();
  });

  it.each([
    ['set aside', PluginStatus.REJECTED],
    ['disabled', PluginStatus.DISABLED],
  ])('serves nothing of a plugin %s', (_case, status) => {
    expect(servedPluginFolders([plugin('a', PluginOrigin.SHIPPED, status)], ROOTS).size).toBe(0);
  });

  it('serves the installed plugin that took the place of a disabled shipped one, and not the shipped one', () => {
    const find = findServedPlugin(
      servedPluginFolders(
        [plugin('a', PluginOrigin.SHIPPED, PluginStatus.DISABLED), plugin('a', PluginOrigin.INSTALLED)],
        ROOTS,
      ),
    );

    expect(find('installed', 'a')).toBe(join(ROOTS.installed, 'a'));
    expect(find('shipped', 'a')).toBeUndefined();
  });

  it('serves nothing of a plugin whose folder is not its identifier, or whose origin is not known', () => {
    const folders = servedPluginFolders(
      [plugin('a', PluginOrigin.SHIPPED, PluginStatus.LOADED, 'copy-of-a'), plugin('b', PluginOrigin.UNSPECIFIED)],
      ROOTS,
    );

    expect(folders.size).toBe(0);
  });
});
