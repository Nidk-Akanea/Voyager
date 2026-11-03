// Maps an address of the voyager://app protocol to a file of a plugin, without ever leaving the folder of that plugin
// and without ever reaching what the interface has no business reading: its manifest, its assemblies (ADR-0015).
import { existsSync } from 'node:fs';
import { realpath } from 'node:fs/promises';
import { basename, dirname, isAbsolute, join, relative, sep } from 'node:path';

import { resolveUserInterfaceFile } from './userInterfaceFiles';

/** Where a plugin comes from, as its addresses name it: a shipped plugin and an installed one may share an identifier. */
export type PluginOriginSegment = 'shipped' | 'installed';

/** The folder of the plugins of each origin. */
export type PluginRoots = Readonly<Record<PluginOriginSegment, string>>;

/** A file of a plugin that the interface may read, with the type to answer it with. */
export interface PluginFile {
  readonly file: string;
  /** The folder of the plugin: the file must stay inside it, even once links are followed. */
  readonly pluginFolder: string;
  readonly contentType: string;
}

/**
 * Finds the folder of a plugin whose files the interface may read, or undefined when it may read none: a plugin that
 * is unknown, set aside or disabled.
 */
export type PluginFolderFinder = (origin: PluginOriginSegment, pluginId: string) => string | undefined;

const PLUGIN_ID = '[a-z][a-z0-9]*(?:-[a-z0-9]+)*';
const ORIGIN = '(shipped|installed)';
// plugins/<origin>/<id>/ui/...: the interface module of the plugin and what it imports.
const INTERFACE_FILE = new RegExp(`^/plugins/${ORIGIN}/(${PLUGIN_ID})/ui/(.+)$`);
// plugins/<origin>/<id>/i18n/<language>/messages.ftl: the translations of the plugin, one file per language.
const MESSAGES_FILE = new RegExp(`^/plugins/${ORIGIN}/(${PLUGIN_ID})/i18n/(fr|en)/messages\\.ftl$`);
const SCRIPT_EXTENSIONS = ['.js', '.mjs'];
// A module script is refused by the browser unless it is served as JavaScript, whatever the system thinks of the extension.
const SCRIPT_TYPE = 'text/javascript';
const MESSAGES_TYPE = 'text/plain; charset=utf-8';
const PARENT_FOLDER = '..';

/**
 * The file an address path points to, or undefined when the interface may not read it.
 * @param findPluginFolder Finds the folder of a plugin the interface may read.
 * @param urlPath Path of the address, such as `/plugins/shipped/diagnostics-clock/ui/index.js`.
 */
export function resolvePluginFile(findPluginFolder: PluginFolderFinder, urlPath: string): PluginFile | undefined {
  const messages = MESSAGES_FILE.exec(urlPath);
  if (messages?.[1] !== undefined && messages[2] !== undefined && messages[3] !== undefined) {
    const pluginFolder = findPluginFolder(messages[1] as PluginOriginSegment, messages[2]);
    return pluginFolder === undefined
      ? undefined
      : {
          file: join(pluginFolder, 'i18n', messages[3], 'messages.ftl'),
          pluginFolder,
          contentType: MESSAGES_TYPE,
        };
  }

  const script = INTERFACE_FILE.exec(urlPath);
  if (script?.[1] === undefined || script[2] === undefined || script[3] === undefined) {
    return undefined;
  }
  const pluginFolder = findPluginFolder(script[1] as PluginOriginSegment, script[2]);
  if (pluginFolder === undefined) {
    return undefined;
  }
  const file = resolveUserInterfaceFile(join(pluginFolder, 'ui'), `/${script[3]}`);
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

/**
 * In development, where the service does not choose: the folder of the plugin under the root of its origin, when it
 * exists. The page only asks for the plugins the service loaded.
 * @param roots The folders of plugins of each origin.
 */
export function findInRoots(roots: PluginRoots): PluginFolderFinder {
  return (origin, pluginId) => {
    const folder = join(roots[origin], pluginId);
    return existsSync(folder) ? folder : undefined;
  };
}
