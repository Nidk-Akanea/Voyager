// The way the plugins of the repository are built: what they leave to the page must be exactly what the page shares,
// or a plugin would carry a second copy of React (spec of the plugin proof, one copy of React on the page).
import { existsSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

import { describe, expect, it } from 'vitest';

import { SHARED_MODULES } from '../../vite.plugins';

const PLUGINS_DIRECTORY = join(import.meta.dirname, '..', '..', '..', '..', '..', 'plugins');

const pluginsWithInterface = readdirSync(PLUGINS_DIRECTORY, { withFileTypes: true })
  .filter((entry) => entry.isDirectory() && existsSync(join(PLUGINS_DIRECTORY, entry.name, 'ui', 'vite.config.ts')))
  .map((entry) => entry.name);

interface PluginBuildConfig {
  readonly build: { readonly rollupOptions: { readonly external: readonly string[] } };
}

describe('the build of the interface of the plugins', () => {
  it('finds the plugins that have an interface', () => {
    expect(pluginsWithInterface).toContain('diagnostics-clock');
  });

  it.each(pluginsWithInterface)(
    '%s leaves to the page everything the page shares, and nothing else',
    async (pluginId) => {
      const url = pathToFileURL(join(PLUGINS_DIRECTORY, pluginId, 'ui', 'vite.config.ts')).href;
      const config = (await import(/* @vite-ignore */ url)) as { default: PluginBuildConfig };

      expect([...config.default.build.rollupOptions.external].sort()).toEqual(Object.keys(SHARED_MODULES).sort());
    },
  );
});
