// Fluent localization of the user interface (ADR-0008): French is the reference language, English is
// the second one, and a missing English message falls back to French.
import { FluentBundle, FluentResource } from '@fluent/bundle';
import { ReactLocalization } from '@fluent/react';

import enAbout from './en/about.ftl?raw';
import enApp from './en/app.ftl?raw';
import frAbout from './fr/about.ftl?raw';
import frApp from './fr/app.ftl?raw';

export const SUPPORTED_LOCALES = ['fr', 'en'] as const;

export type Locale = (typeof SUPPORTED_LOCALES)[number];

export const DEFAULT_LOCALE: Locale = 'fr';

/** Message files of each language; every file must exist in both languages. */
export const MESSAGE_FILES: Readonly<Record<Locale, readonly string[]>> = {
  fr: [frApp, frAbout],
  en: [enApp, enAbout],
};

/** Picks the first supported language among the user's preferences, or French. */
export function pickLocale(preferredLanguages: readonly string[]): Locale {
  for (const language of preferredLanguages) {
    const base = language.toLowerCase().split('-')[0];
    const supported = SUPPORTED_LOCALES.find((locale) => locale === base);
    if (supported) {
      return supported;
    }
  }
  return DEFAULT_LOCALE;
}

/** Builds the localization for a language, with French as the fallback. */
export function createLocalization(locale: Locale): ReactLocalization {
  const locales = locale === DEFAULT_LOCALE ? [locale] : [locale, DEFAULT_LOCALE];
  const bundles = locales.map((bundleLocale) => {
    const bundle = new FluentBundle(bundleLocale);
    for (const source of MESSAGE_FILES[bundleLocale]) {
      bundle.addResource(new FluentResource(source));
    }
    return bundle;
  });
  return new ReactLocalization(bundles);
}
