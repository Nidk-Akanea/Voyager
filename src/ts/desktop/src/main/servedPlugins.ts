// Which plugins the interface may read, and from which folder: the local service decides, from what it loaded
// (ADR-0016, decision 1). The address of a file only names the origin and the plugin; the folder never comes from
// the page.
import { join } from 'node:path';

import { PluginOrigin, PluginStatus, type Plugin } from '@voyager/grpc-client';

import type { PluginFolderFinder, PluginOriginSegment, PluginRoots } from './pluginFiles';

const ORIGIN_SEGMENTS: Readonly<Partial<Record<PluginOrigin, PluginOriginSegment>>> = {
  [PluginOrigin.SHIPPED]: 'shipped',
  [PluginOrigin.INSTALLED]: 'installed',
};

/** The folders of the plugins the interface may read, by `<origin>/<id>`, as the local service listed them. */
export type ServedPlugins = ReadonlyMap<string, string>;

function key(origin: PluginOriginSegment, pluginId: string): string {
  return `${origin}/${pluginId}`;
}

/**
 * The folder of each plugin the local service loaded. A plugin set aside or disabled has none: its interface is never
 * served. Neither has a stray copy whose folder is not its identifier, nor a plugin of an origin this app does not know.
 * @param roots The folders of plugins the app gives the local service.
 */
export function servedPluginFolders(
  plugins: readonly Pick<Plugin, 'id' | 'folder' | 'origin' | 'status'>[],
  roots: PluginRoots,
): ServedPlugins {
  const folders = new Map<string, string>();
  for (const plugin of plugins) {
    const origin = ORIGIN_SEGMENTS[plugin.origin];
    if (plugin.status === PluginStatus.LOADED && plugin.folder === plugin.id && origin !== undefined) {
      folders.set(key(origin, plugin.id), join(roots[origin], plugin.folder));
    }
  }
  return folders;
}

/** Finds a plugin among those the local service loaded. */
export function findServedPlugin(served: ServedPlugins): PluginFolderFinder {
  return (origin, pluginId) => served.get(key(origin, pluginId));
}
