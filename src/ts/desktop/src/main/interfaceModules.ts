// The interface modules of the plugins of a group server (ADR-0020, decision 8; spec of the switch to client-server,
// rule 4): always those of the version the server loaded. The modules shipped with the app are only a cache, used when
// their version and every fingerprint are the same; any other is downloaded from the server, checked file by file, and
// kept on this machine by (origin, plugin, version) in the folder of that server. The pages only ever read checked files.
import { createHash, randomUUID } from 'node:crypto';
import { mkdir, readdir, readFile, rename, rm, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

import { PluginOrigin, PluginStatus, type Plugin } from '@voyager/grpc-client';

import { PLUGIN_ID_PATTERN, VERSION_PATTERN, type PluginOriginSegment } from './pluginFiles';
import { servedPluginKey, type ServedPlugins } from './servedPlugins';

/** The most files the interface of a plugin may have: the same limit as the server (InterfaceFileCatalog.cs). */
export const MAXIMUM_FILES = 200;
/**
 * The largest file of an interface: below the 4 MiB of a gRPC message, with room for its envelope; the same limit as the
 * server (InterfaceFileCatalog.cs).
 */
export const MAXIMUM_FILE_BYTES = 3 * 1024 * 1024;
/** The most the interface of one plugin may weigh. */
export const MAXIMUM_TOTAL_BYTES = 32 * 1024 * 1024;
/** The most the interfaces of all the plugins of one server may weigh on this machine. */
export const MAXIMUM_SERVER_BYTES = 128 * 1024 * 1024;

const PLUGIN_ID = new RegExp(`^${PLUGIN_ID_PATTERN}$`);
const VERSION = new RegExp(`^${VERSION_PATTERN}$`);
const SEGMENT = '[A-Za-z0-9_-][A-Za-z0-9_.-]*';
// What the app serves of a plugin (pluginFiles.ts): scripts under ui, and the translations of each language.
const INTERFACE_PATH = new RegExp(`^ui/(?:${SEGMENT}/)*${SEGMENT}\\.(?:js|mjs)$`);
const MESSAGES_PATH = /^i18n\/(?:fr|en)\/messages\.ftl$/;
const SHA256 = /^[0-9a-f]{64}$/;
// Names Windows keeps for its devices, whatever the extension: a file cannot be written under them.
const RESERVED_NAME = /^(?:con|prn|aux|nul|com\d|lpt\d)(?:\.|$)/i;
// A download under way, in the folder of its plugin: never a version, removed when it fails or at the next cleaning.
const DOWNLOAD_PREFIX = '.download-';
const MANIFEST_FILE = 'plugin.json';
// Windows may hold a folder just written for a moment (an antivirus, the indexer): its renaming is tried again.
const RENAME_ATTEMPTS = 5;
const RENAME_RETRY_MS = 100;

const ORIGIN_SEGMENTS: Readonly<Partial<Record<PluginOrigin, PluginOriginSegment>>> = {
  [PluginOrigin.SHIPPED]: 'shipped',
  [PluginOrigin.INSTALLED]: 'installed',
};

/** What the server says of a plugin, as far as its interface is concerned. */
export type PublishedPlugin = Pick<Plugin, 'id' | 'version' | 'origin' | 'status' | 'interfaceFiles'>;

export interface InterfaceModulesOptions {
  /** The folder of the plugins shipped with the app, one sub-folder per identifier. */
  readonly shippedDirectory: string;
  /** The folder of the downloaded modules of this server: `<origin>/<id>/<version>/...`. */
  readonly cacheDirectory: string;
  /** Downloads one file of the interface of a plugin from the server. */
  readonly download: (plugin: PublishedPlugin, path: string) => Promise<Uint8Array>;
  /** Receives messages that name plugins and versions only, never the address of the server nor a folder. */
  readonly log: { readonly info: (message: string) => void; readonly warn: (message: string) => void };
}

/** A file that is not the one the server listed: the only failure whose message the log keeps. */
class FileMismatchError extends Error {}

interface CheckedFile {
  readonly path: string;
  readonly sha256: string;
  readonly size: number;
}

/** A plugin whose interface the app may download, once its listing is checked. */
interface CheckedPlugin {
  readonly plugin: PublishedPlugin;
  readonly origin: PluginOriginSegment;
  readonly files: readonly CheckedFile[];
  readonly size: number;
}

function fingerprint(content: Uint8Array): string {
  return createHash('sha256').update(content).digest('hex');
}

/** The files the server lists, once checked; the reason when the app must not download them. */
function checkFiles(plugin: PublishedPlugin): { files: readonly CheckedFile[]; size: number } | { refusal: string } {
  if (!PLUGIN_ID.test(plugin.id) || !VERSION.test(plugin.version)) {
    return { refusal: 'its identifier or its version is not valid' };
  }
  if (plugin.interfaceFiles.length > MAXIMUM_FILES) {
    return { refusal: `it has more than ${String(MAXIMUM_FILES)} files` };
  }
  const files: CheckedFile[] = [];
  const paths = new Set<string>();
  let size = 0;
  for (const file of plugin.interfaceFiles) {
    const fileSize = Number(file.size);
    if (
      !(INTERFACE_PATH.test(file.path) || MESSAGES_PATH.test(file.path)) ||
      file.path.split('/').some((name) => RESERVED_NAME.test(name)) ||
      paths.has(file.path.toLowerCase()) ||
      !SHA256.test(file.sha256) ||
      fileSize > MAXIMUM_FILE_BYTES
    ) {
      return { refusal: 'a file has a name, a fingerprint or a size that is not allowed' };
    }
    paths.add(file.path.toLowerCase());
    size += fileSize;
    files.push({ path: file.path, sha256: file.sha256, size: fileSize });
  }
  return size > MAXIMUM_TOTAL_BYTES
    ? { refusal: `its files weigh more than ${String(MAXIMUM_TOTAL_BYTES)} bytes` }
    : { files, size };
}

/** Whether every file is in `folder`, with its fingerprint. */
async function holdsFiles(folder: string, files: readonly CheckedFile[]): Promise<boolean> {
  try {
    for (const file of files) {
      if (fingerprint(await readFile(join(folder, file.path))) !== file.sha256) {
        return false;
      }
    }
    return true;
  } catch {
    return false;
  }
}

/** Whether the app ships this version of the plugin, with the very files the server publishes. */
async function isShipped(folder: string, plugin: PublishedPlugin, files: readonly CheckedFile[]): Promise<boolean> {
  try {
    const manifest = JSON.parse(await readFile(join(folder, MANIFEST_FILE), 'utf8')) as { readonly version?: unknown };
    return manifest.version === plugin.version && (await holdsFiles(folder, files));
  } catch {
    return false;
  }
}

/** Renames a folder, trying again while Windows holds it for a moment. */
async function renameFolder(from: string, to: string): Promise<void> {
  for (let attempt = 1; ; attempt++) {
    try {
      await rename(from, to);
      return;
    } catch (error) {
      const code = (error as { readonly code?: unknown }).code;
      if (attempt >= RENAME_ATTEMPTS || (code !== 'EPERM' && code !== 'EBUSY' && code !== 'EACCES')) {
        throw error;
      }
      await delay(RENAME_RETRY_MS * attempt);
    }
  }
}

/** Downloads every file into a folder of its own, checks it, then makes it the folder of this version. */
async function download(
  checked: CheckedPlugin,
  versionFolder: string,
  options: InterfaceModulesOptions,
): Promise<void> {
  const temporary = join(dirname(versionFolder), `${DOWNLOAD_PREFIX}${randomUUID()}`);
  try {
    for (const file of checked.files) {
      const content = await options.download(checked.plugin, file.path);
      if (content.byteLength !== file.size || fingerprint(content) !== file.sha256) {
        throw new FileMismatchError(`the file ${file.path} is not the one the server listed`);
      }
      const target = join(temporary, file.path);
      await mkdir(dirname(target), { recursive: true });
      await writeFile(target, content);
    }
    await rm(versionFolder, { recursive: true, force: true });
    await renameFolder(temporary, versionFolder);
  } finally {
    await rm(temporary, { recursive: true, force: true });
  }
}

/** The folder to serve the interface of a plugin from; undefined when it has none the app may serve. */
async function prepare(checked: CheckedPlugin, options: InterfaceModulesOptions): Promise<string | undefined> {
  const { plugin, origin, files } = checked;
  // Only a shipped plugin may be the app's own copy: an installed one with the same identifier is another plugin.
  const shipped = join(options.shippedDirectory, plugin.id);
  if (origin === 'shipped' && (await isShipped(shipped, plugin, files))) {
    return shipped;
  }
  const versionFolder = join(options.cacheDirectory, origin, plugin.id, plugin.version);
  if (await holdsFiles(versionFolder, files)) {
    return versionFolder;
  }
  try {
    await download(checked, versionFolder, options);
    options.log.info(`The interface of plugin ${plugin.id} ${plugin.version} was downloaded from the server.`);
    return versionFolder;
  } catch (error) {
    // Only the messages of this module are kept: any other may name the address of the server or a folder of the user.
    const reason = error instanceof FileMismatchError ? error.message : 'the download failed';
    options.log.warn(`The interface of plugin ${plugin.id} ${plugin.version} is not served: ${reason}.`);
    return undefined;
  }
}

/** Removes from the folder of the server the plugins and versions it no longer publishes, and failed downloads. */
async function clean(cacheDirectory: string, kept: ReadonlyMap<string, string>): Promise<void> {
  for (const origin of await readdir(cacheDirectory).catch(() => [])) {
    const originFolder = join(cacheDirectory, origin);
    for (const pluginId of await readdir(originFolder).catch(() => [])) {
      const pluginFolder = join(originFolder, pluginId);
      const version = kept.get(`${origin}/${pluginId}`);
      if (version === undefined) {
        await rm(pluginFolder, { recursive: true, force: true });
        continue;
      }
      for (const entry of await readdir(pluginFolder).catch(() => [])) {
        if (entry !== version) {
          await rm(join(pluginFolder, entry), { recursive: true, force: true });
        }
      }
    }
    if ((await readdir(originFolder).catch(() => [])).length === 0) {
      await rm(originFolder, { recursive: true, force: true });
    }
  }
}

/** The plugins whose interface the app may download, within what one server may take on this machine. */
function checkPlugins(plugins: readonly PublishedPlugin[], options: InterfaceModulesOptions): CheckedPlugin[] {
  const accepted: CheckedPlugin[] = [];
  let serverSize = 0;
  for (const plugin of plugins) {
    const origin = ORIGIN_SEGMENTS[plugin.origin];
    if (plugin.status !== PluginStatus.LOADED || plugin.interfaceFiles.length === 0 || origin === undefined) {
      continue;
    }
    // The identifier comes from the server: it is only written once it is known to be one.
    const name = PLUGIN_ID.test(plugin.id) ? `plugin ${plugin.id}` : 'a plugin';
    const checked = checkFiles(plugin);
    if ('refusal' in checked) {
      options.log.warn(`The interface of ${name} is not served: ${checked.refusal}.`);
    } else if (accepted.some((other) => other.origin === origin && other.plugin.id === plugin.id)) {
      // Two downloads into the same folder would undo each other: only the first listing counts.
      options.log.warn(`The interface of ${name} is listed twice by the server: only the first is served.`);
    } else if (serverSize + checked.size > MAXIMUM_SERVER_BYTES) {
      options.log.warn(`The interface of ${name} is not served: the interfaces of the server weigh too much.`);
    } else {
      serverSize += checked.size;
      accepted.push({ plugin, origin, files: checked.files, size: checked.size });
    }
  }
  return accepted;
}

/**
 * Makes ready the interface of every plugin the server runs, all at once, and tells where to serve each from. A plugin
 * whose files cannot be had, or are not those the server listed, is not served: the page shows it without its panel.
 * @param plugins The plugins the server listed.
 */
export async function prepareInterfaceModules(
  plugins: readonly PublishedPlugin[],
  options: InterfaceModulesOptions,
): Promise<ServedPlugins> {
  const accepted = checkPlugins(plugins, options);
  const folders = await Promise.all(accepted.map((checked) => prepare(checked, options)));
  const served = new Map<string, string>();
  // The version folder is kept even when its download failed this time: a version still published may succeed later.
  const kept = new Map<string, string>();
  accepted.forEach(({ plugin, origin }, index) => {
    kept.set(`${origin}/${plugin.id}`, plugin.version);
    const folder = folders[index];
    if (folder !== undefined) {
      served.set(servedPluginKey(origin, plugin.id, plugin.version), folder);
    }
  });
  await clean(options.cacheDirectory, kept);
  return served;
}
