import { FluentBundle, FluentResource } from '@fluent/bundle';
import { describe, expect, it } from 'vitest';

import { findForeignMessageIds, loadPluginMessages } from './pluginMessages';
import type { PluginRuntime } from './pluginRuntime';

const FRENCH = 'diagnostics-clock-fact-now = Maintenant\ndiagnostics-clock-fact-uptime = Disponibilité';
const ENGLISH = 'diagnostics-clock-fact-now = Now';

function runtimeWith(files: Readonly<Record<string, string>>): PluginRuntime {
  return {
    importModule: () => Promise.reject(new Error('not used')),
    fetchText: (url) => Promise.resolve(files[url]),
    resolve: (path) => `voyager://app/${path}`,
  };
}

const FILES = {
  'voyager://app/plugins/diagnostics-clock/i18n/fr/messages.ftl': FRENCH,
  'voyager://app/plugins/diagnostics-clock/i18n/en/messages.ftl': ENGLISH,
};

describe('findForeignMessageIds', () => {
  it('finds the messages that do not start with the identifier of the plugin', () => {
    const source = 'diagnostics-clock-ok = a\napp-title = Hijacked\n-brand = Term\ndiagnostics-clockwork = b';

    expect(findForeignMessageIds(source, 'diagnostics-clock')).toEqual([
      'app-title',
      '-brand',
      'diagnostics-clockwork',
    ]);
  });
});

describe('loadPluginMessages', () => {
  it('loads the language of the user and French, which the missing messages fall back to', async () => {
    const messages = await loadPluginMessages('diagnostics-clock', 'en', runtimeWith(FILES));

    expect(Object.keys(messages).sort()).toEqual(['en', 'fr']);
    const bundle = new FluentBundle('en');
    bundle.addResource(messages.en ?? new FluentResource(''));
    expect(bundle.getMessage('diagnostics-clock-fact-now')?.value).toBe('Now');
  });

  it('loads French only for French', async () => {
    const messages = await loadPluginMessages('diagnostics-clock', 'fr', runtimeWith(FILES));

    expect(Object.keys(messages)).toEqual(['fr']);
  });

  it('leaves out a language that has no file', async () => {
    const messages = await loadPluginMessages(
      'diagnostics-clock',
      'en',
      runtimeWith({ 'voyager://app/plugins/diagnostics-clock/i18n/fr/messages.ftl': FRENCH }),
    );

    expect(Object.keys(messages)).toEqual(['fr']);
  });

  it('refuses a file that holds a message of the page or of another plugin', async () => {
    const messages = await loadPluginMessages(
      'diagnostics-clock',
      'fr',
      runtimeWith({
        'voyager://app/plugins/diagnostics-clock/i18n/fr/messages.ftl': `${FRENCH}\napp-title = Hijacked`,
      }),
    );

    expect(messages).toEqual({});
  });

  it('leaves out a file that cannot be read', async () => {
    const runtime: PluginRuntime = {
      importModule: () => Promise.reject(new Error('not used')),
      fetchText: () => Promise.reject(new Error('network')),
      resolve: (path) => path,
    };

    expect(await loadPluginMessages('diagnostics-clock', 'fr', runtime)).toEqual({});
  });
});
