// The translations of every plugin of the repository: French and English hold the same messages, and every message
// starts with the identifier of its plugin (spec of the plugin proof, ADR-0015 decision 11).
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

import { FluentResource } from '@fluent/bundle';
import { describe, expect, it } from 'vitest';

import { findForeignMessageIds } from './pluginMessages';

const PLUGINS_DIRECTORY = join(import.meta.dirname, '..', '..', '..', '..', '..', 'plugins');
const LANGUAGES = ['fr', 'en'] as const;

function pluginsWithTranslations(): string[] {
  return readdirSync(PLUGINS_DIRECTORY, { withFileTypes: true })
    .filter((entry) => entry.isDirectory() && existsSync(join(PLUGINS_DIRECTORY, entry.name, 'plugin.json')))
    .map((entry) => entry.name);
}

function readMessages(pluginId: string, language: string): string {
  return readFileSync(join(PLUGINS_DIRECTORY, pluginId, 'i18n', language, 'messages.ftl'), 'utf8');
}

describe('the translations of the plugins', () => {
  it('finds the plugins of the repository', () => {
    expect(pluginsWithTranslations()).toEqual(expect.arrayContaining(['diagnostics-clock', 'diagnostics-runtime']));
  });

  describe.each(pluginsWithTranslations())('%s', (pluginId) => {
    it('has the same messages in French and in English', () => {
      const [french, english] = LANGUAGES.map((language) =>
        new FluentResource(readMessages(pluginId, language)).body.map((entry) => entry.id).sort(),
      );

      expect(english).toEqual(french);
      expect(french?.length).toBeGreaterThan(0);
    });

    it.each(LANGUAGES)('starts every message in %s with its identifier', (language) => {
      expect(findForeignMessageIds(readMessages(pluginId, language), pluginId)).toEqual([]);
    });
  });
});
