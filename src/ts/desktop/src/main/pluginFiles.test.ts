import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

import { resolvePluginFile } from './pluginFiles';

const DIRECTORY = join('voyager', 'plugins');

describe('resolvePluginFile', () => {
  it('serves the interface module of a plugin as JavaScript', () => {
    expect(resolvePluginFile(DIRECTORY, '/plugins/diagnostics-clock/ui/index.js')).toEqual({
      file: join(DIRECTORY, 'diagnostics-clock', 'ui', 'index.js'),
      contentType: 'text/javascript',
    });
  });

  it('serves a module file in a sub-folder of ui', () => {
    expect(resolvePluginFile(DIRECTORY, '/plugins/a/ui/chunks/panel.mjs')?.file).toBe(
      join(DIRECTORY, 'a', 'ui', 'chunks', 'panel.mjs'),
    );
  });

  it('serves the translations of a plugin for a language', () => {
    expect(resolvePluginFile(DIRECTORY, '/plugins/diagnostics-clock/i18n/fr/messages.ftl')).toEqual({
      file: join(DIRECTORY, 'diagnostics-clock', 'i18n', 'fr', 'messages.ftl'),
      contentType: 'text/plain; charset=utf-8',
    });
  });

  it.each([
    ['the manifest', '/plugins/a/plugin.json'],
    ['an assembly', '/plugins/a/Voyager.Plugins.A.dll'],
    ['a file of ui that is not a script', '/plugins/a/ui/secret.json'],
    ['a style sheet', '/plugins/a/ui/style.css'],
    ['the ui folder itself', '/plugins/a/ui/'],
    ['a parent folder', '/plugins/a/ui/../plugin.json'],
    ['an encoded parent folder', '/plugins/a/ui/%2e%2e/plugin.json'],
    ['an encoded backslash', '/plugins/a/ui/..%5cplugin.json'],
    ['an encoded null character', '/plugins/a/ui/index.js%00.txt'],
    ['another plugin through the identifier', '/plugins/../b/ui/index.js'],
    ['an identifier that is not valid', '/plugins/A_B/ui/index.js'],
    ['an identifier with an encoded slash', '/plugins/a%2f..%2fb/ui/index.js'],
    ['a language that is not supported', '/plugins/a/i18n/de/messages.ftl'],
    ['another file of the translations folder', '/plugins/a/i18n/fr/other.ftl'],
    ['a path outside plugins', '/index.html'],
  ])('refuses %s', (_case, urlPath) => {
    expect(resolvePluginFile(DIRECTORY, urlPath)).toBeUndefined();
  });
});
