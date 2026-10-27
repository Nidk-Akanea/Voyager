// What the interface needs from the browser to load a plugin: import its module, read its translations, and tell
// where its files are. Behind an interface so that tests load nothing from the network.
import type { Locale } from '../i18n/localization';

/** How long the page waits for the module or the translations of a plugin before it gives up on them. */
export const PLUGIN_LOAD_TIMEOUT_MS = 10_000;

export interface PluginRuntime {
  /** Imports the interface module at an address. */
  readonly importModule: (url: string) => Promise<unknown>;
  /** Reads a text file at an address; undefined when there is none. */
  readonly fetchText: (url: string) => Promise<string | undefined>;
  /** The address of a path of the protocol of the page, for example `plugins/diagnostics-clock/ui/index.js`. */
  readonly resolve: (path: string) => string;
}

/** Where the interface module of a plugin is, relative to the page. */
export function pluginModulePath(pluginId: string, uiModule: string): string {
  return `plugins/${pluginId}/ui/${uiModule}`;
}

/** Where the translations of a plugin are for a language, relative to the page. */
export function pluginMessagesPath(pluginId: string, locale: Locale): string {
  return `plugins/${pluginId}/i18n/${locale}/messages.ftl`;
}

/** Rejects when `promise` does not settle in `milliseconds`: a plugin that never answers must not hold its card for ever. */
export function withTimeout<T>(promise: Promise<T>, milliseconds: number): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const timer = setTimeout(() => {
      reject(new Error(`No answer in ${String(milliseconds)} ms.`));
    }, milliseconds);
    promise.then(
      (value) => {
        clearTimeout(timer);
        resolve(value);
      },
      (error: unknown) => {
        clearTimeout(timer);
        reject(error instanceof Error ? error : new Error('The call failed.'));
      },
    );
  });
}

/** The real thing: dynamic import, fetch, and addresses relative to the page. */
export const browserPluginRuntime: PluginRuntime = {
  importModule: (url) => import(/* @vite-ignore */ url) as Promise<unknown>,
  fetchText: async (url) => {
    const response = await fetch(url, { signal: AbortSignal.timeout(PLUGIN_LOAD_TIMEOUT_MS) });
    return response.ok ? await response.text() : undefined;
  },
  resolve: (path) => new URL(path, document.baseURI).toString(),
};
