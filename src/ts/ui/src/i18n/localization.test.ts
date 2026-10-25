// @vitest-environment jsdom
import { FluentResource } from '@fluent/bundle';
import { describe, expect, it } from 'vitest';

import { createLocalization, DEFAULT_LOCALE, MESSAGE_FILES, pickLocale, SUPPORTED_LOCALES } from './localization';

const VARIABLE = /\{\s*\$([A-Za-z][\w-]*)/g;

/** Every message id of a language, with the variables its text uses. */
function readMessages(sources: readonly string[]): Map<string, string[]> {
  const messages = new Map<string, string[]>();
  for (const source of sources) {
    const blocks = source.split(/\n(?=[a-z])/);
    for (const block of blocks) {
      const id = /^([a-z][\w-]*)\s*=/.exec(block)?.[1];
      if (id !== undefined) {
        messages.set(id, [...block.matchAll(VARIABLE)].map((match) => match[1] ?? '').sort());
      }
    }
  }
  return messages;
}

describe('message files (ADR-0008)', () => {
  it.each(SUPPORTED_LOCALES)('parse without errors in %s', (locale) => {
    for (const source of MESSAGE_FILES[locale]) {
      const resource = new FluentResource(source);
      expect(resource.body.length).toBeGreaterThan(0);
    }
  });

  it('define the same messages, with the same variables, in French and English', () => {
    const french = readMessages(MESSAGE_FILES.fr);
    const english = readMessages(MESSAGE_FILES.en);

    expect([...english.keys()].sort()).toEqual([...french.keys()].sort());
    for (const [id, variables] of french) {
      expect(english.get(id), `variables of ${id}`).toEqual(variables);
    }
  });
});

describe('pickLocale', () => {
  it('picks the first supported language', () => {
    expect(pickLocale(['de-DE', 'en-GB', 'fr-FR'])).toBe('en');
  });

  it('falls back to French when no language is supported', () => {
    expect(pickLocale(['de-DE', 'es'])).toBe(DEFAULT_LOCALE);
  });
});

describe('createLocalization', () => {
  it('translates into the chosen language', () => {
    expect(createLocalization('en').getString('about-title')).toBe('About Voyager');
    expect(createLocalization('fr').getString('about-title')).toBe('À propos de Voyager');
  });
});
