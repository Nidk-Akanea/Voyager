// Reads the translations of a plugin (`plugins/<id>/i18n/<language>/messages.ftl`, ADR-0015 decision 11). Every
// message of a plugin starts with its identifier, so that a plugin can neither hide a message of the page nor
// borrow the name of another plugin's.
import { FluentResource } from '@fluent/bundle';

import { DEFAULT_LOCALE, type Locale, type PluginMessages } from '../i18n/localization';
import { pluginMessagesPath, type PluginRuntime } from './pluginRuntime';

/**
 * The identifiers of the messages of a source that do not start with `<pluginId>-`. Terms (`-name`) count: a
 * plugin has no use for them, and they could be read by every message of the page.
 */
export function findForeignMessageIds(source: string, pluginId: string): string[] {
  const prefix = `${pluginId}-`;
  return new FluentResource(source).body.map((entry) => entry.id).filter((id) => !id.startsWith(prefix));
}

async function loadMessages(
  pluginId: string,
  locale: Locale,
  runtime: PluginRuntime,
): Promise<FluentResource | undefined> {
  let source: string | undefined;
  try {
    source = await runtime.fetchText(runtime.resolve(pluginMessagesPath(pluginId, locale)));
  } catch {
    // Translations are an extra: a plugin whose file cannot be read is shown with its labels' identifiers.
    return undefined;
  }
  if (source === undefined) {
    return undefined;
  }
  if (findForeignMessageIds(source, pluginId).length > 0) {
    console.warn(`The translations of plugin ${pluginId} were refused: a message does not start with its identifier.`);
    return undefined;
  }
  return new FluentResource(source);
}

/** Loads the translations of a plugin for a language, and for French when the language is another one. */
export async function loadPluginMessages(
  pluginId: string,
  locale: Locale,
  runtime: PluginRuntime,
): Promise<PluginMessages> {
  const locales = locale === DEFAULT_LOCALE ? [locale] : [locale, DEFAULT_LOCALE];
  const loaded = await Promise.all(
    locales.map(async (candidate) => [candidate, await loadMessages(pluginId, candidate, runtime)] as const),
  );
  return Object.fromEntries(
    loaded.filter((entry): entry is readonly [Locale, FluentResource] => entry[1] !== undefined),
  );
}
