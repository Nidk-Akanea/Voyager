// The voyager://app protocol serves the built user interface from disk, with a strict Content
// Security Policy, instead of file:// pages that could read any local file.
import { pathToFileURL } from 'node:url';

import { net, protocol } from 'electron';

import { resolvePluginFile } from './pluginFiles';
import { resolveUserInterfaceFile } from './userInterfaceFiles';

const APP_SCHEME = 'voyager';
const APP_HOST = 'app';
const NOT_FOUND = 404;
const PLUGINS_PATH_PREFIX = '/plugins/';

/** Entry page of the user interface. Its origin is PACKAGED_UI_ORIGIN of the local service. */
export const APP_ENTRY_URL = `${APP_SCHEME}://${APP_HOST}/index.html`;

/** Must run before the app is ready. */
export function registerAppScheme(): void {
  protocol.registerSchemesAsPrivileged([
    { scheme: APP_SCHEME, privileges: { standard: true, secure: true, supportFetchAPI: true } },
  ]);
}

/** Whether a page address belongs to the app's own protocol. */
export function isAppUrl(address: string): boolean {
  const url = URL.parse(address);
  return url?.protocol === `${APP_SCHEME}:` && url.host === APP_HOST;
}

/**
 * Serves the files of `directory`, each with the given Content Security Policy, and the interface modules and
 * translations of the plugins of `pluginsDirectory` (never their manifests or assemblies).
 */
export function serveUserInterface(directory: string, pluginsDirectory: string, contentSecurityPolicy: string): void {
  protocol.handle(APP_SCHEME, async (request) => {
    if (!isAppUrl(request.url)) {
      return new Response(null, { status: NOT_FOUND });
    }
    const { pathname } = new URL(request.url);
    const pluginFile = resolvePluginFile(pluginsDirectory, pathname);
    // Under /plugins/ only what resolvePluginFile allows is served: the built interface never answers for that path.
    const file =
      pluginFile?.file ??
      (pathname.startsWith(PLUGINS_PATH_PREFIX) ? undefined : resolveUserInterfaceFile(directory, pathname));
    if (file === undefined) {
      return new Response(null, { status: NOT_FOUND });
    }
    try {
      const response = await net.fetch(pathToFileURL(file).toString());
      const headers = new Headers(response.headers);
      headers.set('Content-Security-Policy', contentSecurityPolicy);
      if (pluginFile !== undefined) {
        headers.set('Content-Type', pluginFile.contentType);
      }
      return new Response(response.body, { status: response.status, headers });
    } catch {
      // A file that cannot be read is a file that is not there, as far as the page is concerned.
      return new Response(null, { status: NOT_FOUND });
    }
  });
}
