// Which plugins the pages of a server view may read, and from which folder: the server decides which plugins and which
// versions, and the app where their checked files are (interfaceModules.ts). The address of a file only names the
// origin, the plugin and the version; the folder never comes from the page.
import type { PluginFolderFinder, PluginOriginSegment } from './pluginFiles';

/** The folders of the plugins the interface may read, by `<origin>/<id>/<version>`. */
export type ServedPlugins = ReadonlyMap<string, string>;

/** The key of a plugin in {@link ServedPlugins}. */
export function servedPluginKey(origin: PluginOriginSegment, pluginId: string, version: string): string {
  return `${origin}/${pluginId}/${version}`;
}

/** Finds a plugin among those the interface may read. */
export function findServedPlugin(served: ServedPlugins): PluginFolderFinder {
  return (origin, pluginId, version) => served.get(servedPluginKey(origin, pluginId, version));
}
