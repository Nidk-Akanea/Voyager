import { mkdir, mkdtemp, realpath, rm, symlink, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { pluginAddressOf, resolvePluginFile, resolveRealPluginFile, type PluginOriginSegment } from './pluginFiles';

const DIRECTORY = join('voyager', 'plugins');

// Every plugin is served from a folder named after its origin, identifier and version: what the server decides is tested
// elsewhere.
const everyPlugin = (origin: PluginOriginSegment, pluginId: string, version: string) =>
  join(DIRECTORY, origin, pluginId, version);

describe('resolvePluginFile', () => {
  it('serves the interface module of a plugin as JavaScript', () => {
    expect(resolvePluginFile(everyPlugin, '/plugins/shipped/diagnostics-clock/1.2.0/ui/index.js')).toEqual({
      file: join(DIRECTORY, 'shipped', 'diagnostics-clock', '1.2.0', 'ui', 'index.js'),
      pluginFolder: join(DIRECTORY, 'shipped', 'diagnostics-clock', '1.2.0'),
      contentType: 'text/javascript',
    });
  });

  it('serves a module file in a sub-folder of ui', () => {
    expect(resolvePluginFile(everyPlugin, '/plugins/installed/a/1.0.0-beta.2/ui/chunks/panel.mjs')?.file).toBe(
      join(DIRECTORY, 'installed', 'a', '1.0.0-beta.2', 'ui', 'chunks', 'panel.mjs'),
    );
  });

  it('serves the translations of a plugin for a language', () => {
    expect(resolvePluginFile(everyPlugin, '/plugins/shipped/diagnostics-clock/1.2.0/i18n/fr/messages.ftl')).toEqual({
      file: join(DIRECTORY, 'shipped', 'diagnostics-clock', '1.2.0', 'i18n', 'fr', 'messages.ftl'),
      pluginFolder: join(DIRECTORY, 'shipped', 'diagnostics-clock', '1.2.0'),
      contentType: 'text/plain; charset=utf-8',
    });
  });

  it('asks for the origin, the identifier and the version the address names', () => {
    const asked: string[] = [];
    resolvePluginFile((origin, pluginId, version) => {
      asked.push(`${origin}/${pluginId}/${version}`);
      return undefined;
    }, '/plugins/installed/a/2.0.1+build.7/ui/index.js');

    expect(asked).toEqual(['installed/a/2.0.1+build.7']);
  });

  it.each([
    ['the module', '/plugins/shipped/a/1.0.0/ui/index.js'],
    ['the translations', '/plugins/shipped/a/1.0.0/i18n/fr/messages.ftl'],
  ])('refuses %s of a plugin that may not be served', (_case, urlPath) => {
    expect(resolvePluginFile(() => undefined, urlPath)).toBeUndefined();
  });

  it.each([
    ['an address without an origin', '/plugins/a/1.0.0/ui/index.js'],
    ['an origin that does not exist', '/plugins/group/a/1.0.0/ui/index.js'],
    ['the manifest', '/plugins/shipped/a/1.0.0/plugin.json'],
    ['an assembly', '/plugins/shipped/a/1.0.0/Voyager.Plugins.A.dll'],
    ['a file of ui that is not a script', '/plugins/shipped/a/1.0.0/ui/secret.json'],
    ['a style sheet', '/plugins/shipped/a/1.0.0/ui/style.css'],
    ['the ui folder itself', '/plugins/shipped/a/1.0.0/ui/'],
    ['a parent folder', '/plugins/shipped/a/1.0.0/ui/../plugin.json'],
    ['an encoded parent folder', '/plugins/shipped/a/1.0.0/ui/%2e%2e/plugin.json'],
    ['an encoded backslash', '/plugins/shipped/a/1.0.0/ui/..%5cplugin.json'],
    ['an encoded null character', '/plugins/shipped/a/1.0.0/ui/index.js%00.txt'],
    ['another plugin through the identifier', '/plugins/shipped/../b/1.0.0/ui/index.js'],
    ['an identifier that is not valid', '/plugins/shipped/A_B/1.0.0/ui/index.js'],
    ['an identifier with an encoded slash', '/plugins/shipped/a%2f..%2fb/1.0.0/ui/index.js'],
    ['a language that is not supported', '/plugins/shipped/a/1.0.0/i18n/de/messages.ftl'],
    ['another file of the translations folder', '/plugins/shipped/a/1.0.0/i18n/fr/other.ftl'],
    ['an address without a version', '/plugins/shipped/a/ui/index.js'],
    ['a version that is not one', '/plugins/shipped/a/latest/ui/index.js'],
    ['a version with a parent folder', '/plugins/shipped/a/1.0.0-../ui/index.js'],
    ['a path outside plugins', '/index.html'],
  ])('refuses %s', (_case, urlPath) => {
    expect(resolvePluginFile(everyPlugin, urlPath)).toBeUndefined();
  });
});

describe('pluginAddressOf', () => {
  it('reads the origin, the identifier and the version of the plugin an address names', () => {
    expect(pluginAddressOf('/plugins/shipped/diagnostics-clock/1.2.0-alpha.3/ui/index.js')).toEqual({
      origin: 'shipped',
      pluginId: 'diagnostics-clock',
      version: '1.2.0-alpha.3',
    });
  });

  it('names no plugin outside the folders of the plugins', () => {
    expect(pluginAddressOf('/assets/index.js')).toBeUndefined();
  });
});

describe('resolveRealPluginFile', () => {
  let root: string;

  beforeEach(async () => {
    root = await mkdtemp(join(tmpdir(), 'voyager-plugin-files-'));
    await mkdir(join(root, 'plugins', 'a', 'ui'), { recursive: true });
    await writeFile(join(root, 'plugins', 'a', 'ui', 'index.js'), 'export {};');
    await mkdir(join(root, 'elsewhere', 'ui'), { recursive: true });
    await writeFile(join(root, 'elsewhere', 'secret.js'), 'export {};');
    await writeFile(join(root, 'elsewhere', 'ui', 'index.js'), 'export {};');
  });

  afterEach(async () => {
    await rm(root, { recursive: true, force: true });
  });

  const pluginFile = (pluginId: string, ...path: string[]) => ({
    file: join(root, 'plugins', pluginId, ...path),
    pluginFolder: join(root, 'plugins', pluginId),
    contentType: 'text/javascript',
  });

  // The temporary folder may itself be reached through a link (a short name on Windows): real paths are compared.
  it('gives the real path of a file of the plugin', async () => {
    expect(await resolveRealPluginFile(pluginFile('a', 'ui', 'index.js'))).toBe(
      join(await realpath(root), 'plugins', 'a', 'ui', 'index.js'),
    );
  });

  it('refuses a file that is not there', async () => {
    expect(await resolveRealPluginFile(pluginFile('a', 'ui', 'missing.js'))).toBeUndefined();
  });

  it('refuses a file reached through a link that leaves the folder of the plugin', async () => {
    // A junction needs no particular right on Windows, unlike a symbolic link.
    await symlink(join(root, 'elsewhere'), join(root, 'plugins', 'a', 'ui', 'linked'), 'junction');

    expect(await resolveRealPluginFile(pluginFile('a', 'ui', 'linked', 'secret.js'))).toBeUndefined();
  });

  it('refuses the files of a plugin whose folder is itself a link to elsewhere', async () => {
    await symlink(join(root, 'elsewhere'), join(root, 'plugins', 'b'), 'junction');

    expect(await resolveRealPluginFile(pluginFile('b', 'ui', 'index.js'))).toBeUndefined();
  });
});
