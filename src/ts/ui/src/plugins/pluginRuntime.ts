// What the interface needs from the browser to load a plugin: import its module, read its translations, and tell
// where its files are. Behind an interface so that tests load nothing from the network.
import { PluginOrigin } from '@voyager/grpc-client';

import type { Locale } from '../i18n/localization';

/** How long the page waits for the module or the translations of a plugin before it gives up on them. */
export const PLUGIN_LOAD_TIMEOUT_MS = 10_000;

export interface PluginRuntime {
  /** Imports the interface module at an address. */
  readonly importModule: (url: string) => Promise<unknown>;
  /** Reads a text file at an address; undefined when there is none. */
  readonly fetchText: (url: string) => Promise<string | undefined>;
  /** The address of a path of the app's protocol, for example `plugins/shipped/diagnostics-clock/1.2.0/ui/index.js`. */
  readonly resolve: (path: string) => string;
}

/**
 * What tells the files of a plugin apart from those of another: a shipped plugin and an installed one may share an
 * identifier, so the address of a file names the origin too (ADR-0016, decision 1); and the page uses the files of the
 * version the server loaded, so it names the version (ADR-0020, decision 8).
 */
export interface PluginAddress {
  readonly id: string;
  readonly origin: PluginOrigin;
  readonly version: string;
}

const ORIGIN_SEGMENTS: Readonly<Partial<Record<PluginOrigin, string>>> = {
  [PluginOrigin.SHIPPED]: 'shipped',
  [PluginOrigin.INSTALLED]: 'installed',
};
// An origin or a version this page does not know gets an address that serves nothing, never the files of another plugin.
const UNKNOWN_SEGMENT = 'unknown';
// A semantic version, as the builds of Voyager give them: nothing else may become a segment of an address.
const VERSION = /^\d+\.\d+\.\d+(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$/;
// The files of the plugins come from the app's protocol, which serves those the server loaded, even in development where
// Vite serves the page (src/ts/desktop/src/main/interfaceModules.ts).
const APP_BASE_URL = 'voyager://app/';

/** Where the files of a version of a plugin are, under the app's protocol: `plugins/<origin>/<id>/<version>`. */
function pluginFolderPath(plugin: PluginAddress): string {
  const version = VERSION.test(plugin.version) ? plugin.version : UNKNOWN_SEGMENT;
  return `plugins/${ORIGIN_SEGMENTS[plugin.origin] ?? UNKNOWN_SEGMENT}/${plugin.id}/${version}`;
}

/** Where the interface module of a plugin is, relative to the page. */
export function pluginModulePath(plugin: PluginAddress, uiModule: string): string {
  return `${pluginFolderPath(plugin)}/ui/${uiModule}`;
}

/** Where the translations of a plugin are for a language, relative to the page. */
export function pluginMessagesPath(plugin: PluginAddress, locale: Locale): string {
  return `${pluginFolderPath(plugin)}/i18n/${locale}/messages.ftl`;
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

/** The real thing: dynamic import, fetch, and addresses of the app's protocol. */
export const browserPluginRuntime: PluginRuntime = {
  importModule: (url) => import(/* @vite-ignore */ url) as Promise<unknown>,
  fetchText: async (url) => {
    const response = await fetch(url, { signal: AbortSignal.timeout(PLUGIN_LOAD_TIMEOUT_MS) });
    return response.ok ? await response.text() : undefined;
  },
  resolve: (path) => new URL(path, APP_BASE_URL).toString(),
};
