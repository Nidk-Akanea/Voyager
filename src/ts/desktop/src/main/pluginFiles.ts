// Maps an address of the voyager://app protocol to a file of a plugin, without ever leaving the folder of that plugin
// and without ever reaching what the interface has no business reading: its manifest, its assemblies (ADR-0015).
import { realpath } from 'node:fs/promises';
import { basename, dirname, isAbsolute, join, relative, sep } from 'node:path';

import { resolveUserInterfaceFile } from './userInterfaceFiles';

/** Where a plugin comes from, as its addresses name it: a shipped plugin and an installed one may share an identifier. */
export type PluginOriginSegment = 'shipped' | 'installed';

/** A file of a plugin that the interface may read, with the type to answer it with. */
export interface PluginFile {
  readonly file: string;
  /** The folder of the plugin: the file must stay inside it, even once links are followed. */
  readonly pluginFolder: string;
  readonly contentType: string;
}

/** The plugin an address names: where it comes from, its identifier and its version. */
export interface PluginAddress {
  readonly origin: PluginOriginSegment;
  readonly pluginId: string;
  readonly version: string;
}

/**
 * Finds the folder of a version of a plugin whose files the interface may read, or undefined when it may read none: a
 * plugin that is unknown, not in use, or in another version on the server.
 */
export type PluginFolderFinder = (origin: PluginOriginSegment, pluginId: string, version: string) => string | undefined;

/** The pattern of a plugin identifier. */
export const PLUGIN_ID_PATTERN = '[a-z][a-z0-9]*(?:-[a-z0-9]+)*';
/** The pattern of a version of a plugin: a semantic version, as the builds of Voyager give them. */
export const VERSION_PATTERN = String.raw`\d+\.\d+\.\d+(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?`;
// plugins/<origin>/<id>/<version>/: the folder of a version of a plugin.
const PLUGIN = `^/plugins/(?<origin>shipped|installed)/(?<pluginId>${PLUGIN_ID_PATTERN})/(?<version>${VERSION_PATTERN})/`;
const ADDRESS = new RegExp(PLUGIN);
// .../ui/...: the interface module of the plugin and what it imports.
const INTERFACE_FILE = new RegExp(`${PLUGIN}ui/(?<path>.+)$`);
// .../i18n/<language>/messages.ftl: the translations of the plugin, one file per language.
const MESSAGES_FILE = new RegExp(String.raw`${PLUGIN}i18n/(?<language>fr|en)/messages\.ftl$`);
const SCRIPT_EXTENSIONS = ['.js', '.mjs'];
// A module script is refused by the browser unless it is served as JavaScript, whatever the system thinks of the extension.
const SCRIPT_TYPE = 'text/javascript';
const MESSAGES_TYPE = 'text/plain; charset=utf-8';
const PARENT_FOLDER = '..';

/**
 * The plugin an address path names, or undefined when it names none.
 * @param urlPath Path of the address, such as `/plugins/shipped/diagnostics-clock/1.2.0/ui/index.js`.
 */
export function pluginAddressOf(urlPath: string): PluginAddress | undefined {
  const groups = ADDRESS.exec(urlPath)?.groups;
  const { origin, pluginId, version } = groups ?? {};
  return origin === undefined || pluginId === undefined || version === undefined
    ? undefined
    : { origin: origin as PluginOriginSegment, pluginId, version };
}

/**
 * The file an address path points to, or undefined when the interface may not read it.
 * @param findPluginFolder Finds the folder of a version of a plugin the interface may read.
 * @param urlPath Path of the address, such as `/plugins/shipped/diagnostics-clock/1.2.0/ui/index.js`.
 */
export function resolvePluginFile(findPluginFolder: PluginFolderFinder, urlPath: string): PluginFile | undefined {
  const address = pluginAddressOf(urlPath);
  if (address === undefined) {
    return undefined;
  }
  const messages = MESSAGES_FILE.exec(urlPath)?.groups?.language;
  const script = INTERFACE_FILE.exec(urlPath)?.groups?.path;
  if (messages === undefined && script === undefined) {
    return undefined;
  }
  const pluginFolder = findPluginFolder(address.origin, address.pluginId, address.version);
  if (pluginFolder === undefined) {
    return undefined;
  }
  if (messages !== undefined) {
    return { file: join(pluginFolder, 'i18n', messages, 'messages.ftl'), pluginFolder, contentType: MESSAGES_TYPE };
  }
  const file = resolveUserInterfaceFile(join(pluginFolder, 'ui'), `/${script ?? ''}`);
  if (file === undefined || !SCRIPT_EXTENSIONS.some((extension) => file.endsWith(extension))) {
    return undefined;
  }
  return { file, pluginFolder, contentType: SCRIPT_TYPE };
}

function isInside(folder: string, file: string): boolean {
  const path = relative(folder, file);
  return path !== '' && path !== PARENT_FOLDER && !path.startsWith(`${PARENT_FOLDER}${sep}`) && !isAbsolute(path);
}

/**
 * The real path of a file of a plugin, once links and junctions are followed, or undefined when it is not really
 * inside the folder of that plugin: the path alone cannot tell (ADR-0016, decision 1). The folder of the plugin
 * itself may not be a link either. The file must then be read at the path returned, never at the one checked, so that
 * a link changed in between leads nowhere else.
 */
export async function resolveRealPluginFile(pluginFile: PluginFile): Promise<string | undefined> {
  try {
    const [file, folder, parent] = await Promise.all([
      realpath(pluginFile.file),
      realpath(pluginFile.pluginFolder),
      realpath(dirname(pluginFile.pluginFolder)),
    ]);
    const folderIsReal = folder === join(parent, basename(pluginFile.pluginFolder));
    return folderIsReal && isInside(folder, file) ? file : undefined;
  } catch {
    return undefined;
  }
}
