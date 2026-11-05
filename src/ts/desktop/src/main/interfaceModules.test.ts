import { createHash } from 'node:crypto';
import { mkdir, mkdtemp, readdir, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { PluginOrigin, PluginStatus } from '@voyager/grpc-client';

import { MAXIMUM_FILE_BYTES, prepareInterfaceModules, type PublishedPlugin } from './interfaceModules';
import { servedPluginKey } from './servedPlugins';

const MODULE = 'export function register() {}';
const MESSAGES = 'clock-title = Horloge';

function sha256(content: string): string {
  return createHash('sha256').update(content).digest('hex');
}

function file(path: string, content: string) {
  return { path, sha256: sha256(content), size: BigInt(Buffer.byteLength(content)) };
}

/** A plugin as the server lists it, with its module and its French translations. */
function published(version: string, overrides: Partial<PublishedPlugin> = {}): PublishedPlugin {
  return {
    id: 'diagnostics-clock',
    version,
    origin: PluginOrigin.SHIPPED,
    status: PluginStatus.LOADED,
    interfaceFiles: [file('ui/index.js', MODULE), file('i18n/fr/messages.ftl', MESSAGES)],
    ...overrides,
  } as PublishedPlugin;
}

const SERVED_FILES: Readonly<Record<string, string>> = { 'ui/index.js': MODULE, 'i18n/fr/messages.ftl': MESSAGES };

describe('prepareInterfaceModules', () => {
  let root: string;
  let shippedDirectory: string;
  let cacheDirectory: string;
  const log = { info: vi.fn(), warn: vi.fn() };

  /** The server sends the files it listed, or other content for the paths in `tampered`. */
  function downloader(tampered: Readonly<Record<string, string>> = {}) {
    return vi.fn((_plugin: PublishedPlugin, path: string) =>
      Promise.resolve(new TextEncoder().encode(tampered[path] ?? SERVED_FILES[path] ?? '')),
    );
  }

  async function ship(version: string): Promise<void> {
    const folder = join(shippedDirectory, 'diagnostics-clock');
    await mkdir(join(folder, 'ui'), { recursive: true });
    await mkdir(join(folder, 'i18n', 'fr'), { recursive: true });
    await writeFile(join(folder, 'plugin.json'), JSON.stringify({ id: 'diagnostics-clock', version }));
    await writeFile(join(folder, 'ui', 'index.js'), MODULE);
    await writeFile(join(folder, 'i18n', 'fr', 'messages.ftl'), MESSAGES);
  }

  beforeEach(async () => {
    root = await mkdtemp(join(tmpdir(), 'voyager-interface-modules-'));
    shippedDirectory = join(root, 'shipped');
    cacheDirectory = join(root, 'servers', 'family', 'plugins');
    log.info.mockClear();
    log.warn.mockClear();
  });

  afterEach(async () => {
    await rm(root, { recursive: true, force: true });
  });

  it('serves the copy shipped with the app when it is the same version, with the same files', async () => {
    await ship('1.2.0');
    const download = downloader();

    const served = await prepareInterfaceModules([published('1.2.0')], {
      shippedDirectory,
      cacheDirectory,
      download,
      log,
    });

    expect(served.get(servedPluginKey('shipped', 'diagnostics-clock', '1.2.0'))).toBe(
      join(shippedDirectory, 'diagnostics-clock'),
    );
    expect(download).not.toHaveBeenCalled();
  });

  it('downloads another version, keeps it by plugin and version, and uses it again without downloading', async () => {
    await ship('1.2.0');
    const download = downloader();
    const options = { shippedDirectory, cacheDirectory, download, log };

    const served = await prepareInterfaceModules([published('1.3.0')], options);

    const folder = join(cacheDirectory, 'shipped', 'diagnostics-clock', '1.3.0');
    expect(served.get(servedPluginKey('shipped', 'diagnostics-clock', '1.3.0'))).toBe(folder);
    expect(await readFile(join(folder, 'ui', 'index.js'), 'utf8')).toBe(MODULE);
    expect(download).toHaveBeenCalledTimes(2);

    download.mockClear();
    await prepareInterfaceModules([published('1.3.0')], options);
    expect(download).not.toHaveBeenCalled();
  });

  it('downloads again when the shipped copy has the version but not the same files', async () => {
    await ship('1.2.0');
    await writeFile(join(shippedDirectory, 'diagnostics-clock', 'ui', 'index.js'), 'export const changed = 1;');
    const download = downloader();

    const served = await prepareInterfaceModules([published('1.2.0')], {
      shippedDirectory,
      cacheDirectory,
      download,
      log,
    });

    expect(served.get(servedPluginKey('shipped', 'diagnostics-clock', '1.2.0'))).toBe(
      join(cacheDirectory, 'shipped', 'diagnostics-clock', '1.2.0'),
    );
  });

  it('serves nothing of a plugin whose downloaded file is not the one listed, and keeps nothing of it', async () => {
    const served = await prepareInterfaceModules([published('1.3.0')], {
      shippedDirectory,
      cacheDirectory,
      download: downloader({ 'ui/index.js': 'alert("not the module")' }),
      log,
    });

    expect(served.size).toBe(0);
    expect(log.warn).toHaveBeenCalledWith(expect.stringContaining('ui/index.js is not the one the server listed'));
    expect(await readdir(join(cacheDirectory, 'shipped', 'diagnostics-clock')).catch(() => [])).toEqual([]);
  });

  it('serves nothing of a plugin whose download fails', async () => {
    const served = await prepareInterfaceModules([published('1.3.0')], {
      shippedDirectory,
      cacheDirectory,
      download: () => Promise.reject(new Error('[unavailable] fetch failed for https://family.example.org')),
      log,
    });

    expect(served.size).toBe(0);
    // The message of the error may name the address of the server: it is not logged.
    expect(log.warn).toHaveBeenCalledWith(expect.stringContaining('the download failed'));
  });

  it.each([
    ['a path out of the plugin', { interfaceFiles: [file('ui/../plugin.json', MODULE)] }],
    ['a file that is not a script', { interfaceFiles: [file('ui/style.css', MODULE)] }],
    ['a name Windows keeps for a device', { interfaceFiles: [file('ui/con.js', MODULE)] }],
    ['a hidden file', { interfaceFiles: [file('ui/.hidden.js', MODULE)] }],
    [
      'a file too large',
      { interfaceFiles: [{ ...file('ui/index.js', MODULE), size: BigInt(MAXIMUM_FILE_BYTES + 1) }] },
    ],
    ['a fingerprint that is not one', { interfaceFiles: [{ ...file('ui/index.js', MODULE), sha256: 'abc' }] }],
    ['the same file twice', { interfaceFiles: [file('ui/index.js', MODULE), file('ui/INDEX.js', MODULE)] }],
    ['a version that is not one', { version: '../1.0.0' }],
    ['an identifier that is not one', { id: '../elsewhere' }],
  ] as const)('downloads nothing of a plugin listed with %s', async (_case, overrides) => {
    const download = downloader();

    const served = await prepareInterfaceModules([published('1.3.0', overrides as Partial<PublishedPlugin>)], {
      shippedDirectory,
      cacheDirectory,
      download,
      log,
    });

    expect(served.size).toBe(0);
    expect(download).not.toHaveBeenCalled();
  });

  it('ignores the plugins that are not in use, or have no interface', async () => {
    const download = downloader();

    const served = await prepareInterfaceModules(
      [published('1.3.0', { status: PluginStatus.REJECTED }), published('1.3.0', { interfaceFiles: [] })],
      { shippedDirectory, cacheDirectory, download, log },
    );

    expect(served.size).toBe(0);
    expect(download).not.toHaveBeenCalled();
  });

  it('keeps a shipped plugin and an installed one with the same identifier apart', async () => {
    const installedModule = 'export function register() { /* installed */ }';
    const download = vi.fn((plugin: PublishedPlugin, path: string) =>
      Promise.resolve(
        new TextEncoder().encode(
          plugin.origin === PluginOrigin.INSTALLED && path === 'ui/index.js'
            ? installedModule
            : (SERVED_FILES[path] ?? ''),
        ),
      ),
    );
    const installed = published('1.3.0', {
      origin: PluginOrigin.INSTALLED,
      interfaceFiles: [file('ui/index.js', installedModule)],
    } as Partial<PublishedPlugin>);

    const served = await prepareInterfaceModules([published('1.3.0'), installed], {
      shippedDirectory,
      cacheDirectory,
      download,
      log,
    });

    const shippedFolder = served.get(servedPluginKey('shipped', 'diagnostics-clock', '1.3.0')) ?? '';
    const installedFolder = served.get(servedPluginKey('installed', 'diagnostics-clock', '1.3.0')) ?? '';
    expect(shippedFolder).not.toBe(installedFolder);
    expect(await readFile(join(shippedFolder, 'ui', 'index.js'), 'utf8')).toBe(MODULE);
    expect(await readFile(join(installedFolder, 'ui', 'index.js'), 'utf8')).toBe(installedModule);
  });

  it('never takes the copy shipped with the app for an installed plugin of the same identifier', async () => {
    await ship('1.2.0');
    const download = downloader();

    const served = await prepareInterfaceModules([published('1.2.0', { origin: PluginOrigin.INSTALLED })], {
      shippedDirectory,
      cacheDirectory,
      download,
      log,
    });

    expect(served.get(servedPluginKey('installed', 'diagnostics-clock', '1.2.0'))).toBe(
      join(cacheDirectory, 'installed', 'diagnostics-clock', '1.2.0'),
    );
    expect(download).toHaveBeenCalled();
  });

  it('serves nothing of a plugin whose downloaded file has another size than the one listed', async () => {
    const served = await prepareInterfaceModules([published('1.3.0')], {
      shippedDirectory,
      cacheDirectory,
      download: downloader({ 'ui/index.js': `${MODULE} ` }),
      log,
    });

    expect(served.size).toBe(0);
  });

  it('serves the plugins it could download when another one fails', async () => {
    const other = published('2.0.0', { id: 'diagnostics-runtime' });
    const download = vi.fn((plugin: PublishedPlugin, path: string) =>
      plugin.id === 'diagnostics-runtime'
        ? Promise.reject(new Error('unavailable'))
        : Promise.resolve(new TextEncoder().encode(SERVED_FILES[path] ?? '')),
    );

    const served = await prepareInterfaceModules([published('1.3.0'), other], {
      shippedDirectory,
      cacheDirectory,
      download,
      log,
    });

    expect([...served.keys()]).toEqual([servedPluginKey('shipped', 'diagnostics-clock', '1.3.0')]);
  });

  it('removes the versions and the plugins the server no longer publishes', async () => {
    await mkdir(join(cacheDirectory, 'shipped', 'diagnostics-clock', '1.1.0'), { recursive: true });
    await mkdir(join(cacheDirectory, 'shipped', 'diagnostics-clock', '.download-left-behind'), { recursive: true });
    await mkdir(join(cacheDirectory, 'installed', 'itinerary-map', '2.0.0'), { recursive: true });

    await prepareInterfaceModules([published('1.3.0')], {
      shippedDirectory,
      cacheDirectory,
      download: downloader(),
      log,
    });

    expect(await readdir(cacheDirectory)).toEqual(['shipped']);
    expect(await readdir(join(cacheDirectory, 'shipped'))).toEqual(['diagnostics-clock']);
    expect(await readdir(join(cacheDirectory, 'shipped', 'diagnostics-clock'))).toEqual(['1.3.0']);
  });
});
