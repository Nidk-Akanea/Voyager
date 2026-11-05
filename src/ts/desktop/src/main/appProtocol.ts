// The voyager://app protocol serves the built user interface from disk, with a strict Content
// Security Policy, instead of file:// pages that could read any local file.
import { pathToFileURL } from 'node:url';

import { net, protocol, type Protocol } from 'electron';

import {
  pluginAddressOf,
  resolvePluginFile,
  resolveRealPluginFile,
  type PluginAddress,
  type PluginFile,
} from './pluginFiles';
import { findServedPlugin, type ServedPlugins } from './servedPlugins';
import { SERVER_API_PATH_PREFIX } from './serverApi';
import { resolveUserInterfaceFile } from './userInterfaceFiles';

const APP_SCHEME = 'voyager';
const APP_HOST = 'app';
const NOT_FOUND = 404;
const PLUGINS_PATH_PREFIX = '/plugins/';

/** Entry page of the user interface. Its origin is PACKAGED_UI_ORIGIN of the local service. */
export const APP_ENTRY_URL = `${APP_SCHEME}://${APP_HOST}/index.html`;

/**
 * Must run before the app is ready.
 * @param allowOtherOrigins Lets a page of another origin call the protocol under CORS: in development only, where the
 * page comes from Vite and calls the servers through this protocol.
 */
export function registerAppScheme(allowOtherOrigins: boolean): void {
  protocol.registerSchemesAsPrivileged([
    {
      scheme: APP_SCHEME,
      privileges: { standard: true, secure: true, supportFetchAPI: true, corsEnabled: allowOtherOrigins },
    },
  ]);
}

/** Whether a page address belongs to the app's own protocol. */
export function isAppUrl(address: string): boolean {
  const url = URL.parse(address);
  return url?.protocol === `${APP_SCHEME}:` && url.host === APP_HOST;
}

export interface UserInterfaceSource {
  /** The folder of the built user interface. */
  readonly directory: string;
  /**
   * The folder of each plugin whose files may be served, by origin, identifier and version, as the server last listed
   * them; given the plugin a page asks for, so that a version not seen yet makes it ask the server again.
   */
  readonly servedPlugins: (wanted: PluginAddress) => Promise<ServedPlugins>;
  readonly contentSecurityPolicy: string;
  /** Answers the calls of the page under /api/ (serverApi.ts). */
  readonly forwardToServer: (request: Request) => Promise<Response>;
  /**
   * The origins of pages, other than the app's own, that may read the files of the plugins: in development, the page
   * comes from Vite and reads them through this protocol.
   */
  readonly allowedOrigins: readonly string[];
}

/**
 * Serves the files of the built user interface, each with the given Content Security Policy, the interface modules and
 * translations of the plugins the server loaded (never their manifests or assemblies), and forwards the calls of the
 * page to its server.
 * @param target The protocol of the session that serves the page: each server view has its own (serverViews.ts).
 */
export function serveUserInterface(target: Pick<Protocol, 'handle'>, source: UserInterfaceSource): void {
  const { directory, servedPlugins, contentSecurityPolicy, forwardToServer, allowedOrigins } = source;
  target.handle(APP_SCHEME, async (request) => {
    if (!isAppUrl(request.url)) {
      return new Response(null, { status: NOT_FOUND });
    }
    const { pathname } = new URL(request.url);
    if (pathname.startsWith(SERVER_API_PATH_PREFIX)) {
      return forwardToServer(request);
    }
    let pluginFile: PluginFile | undefined;
    let realPluginFile: string | undefined;
    // Under /plugins/ only what resolvePluginFile allows is served: the built interface never answers for that path.
    if (pathname.startsWith(PLUGINS_PATH_PREFIX)) {
      const address = pluginAddressOf(pathname);
      pluginFile =
        address === undefined ? undefined : resolvePluginFile(findServedPlugin(await servedPlugins(address)), pathname);
      realPluginFile = pluginFile === undefined ? undefined : await resolveRealPluginFile(pluginFile);
      if (realPluginFile === undefined) {
        return new Response(null, { status: NOT_FOUND });
      }
    }
    const file = realPluginFile ?? resolveUserInterfaceFile(directory, pathname);
    if (file === undefined) {
      return new Response(null, { status: NOT_FOUND });
    }
    try {
      const response = await net.fetch(pathToFileURL(file).toString());
      const headers = new Headers(response.headers);
      headers.set('Content-Security-Policy', contentSecurityPolicy);
      if (pluginFile !== undefined) {
        headers.set('Content-Type', pluginFile.contentType);
        // A module script is read under CORS: a page of an allowed origin may read it, and no other.
        const origin = request.headers.get('Origin');
        if (origin !== null && allowedOrigins.includes(origin)) {
          headers.set('Access-Control-Allow-Origin', origin);
          headers.set('Vary', 'Origin');
        }
      }
      return new Response(response.body, { status: response.status, headers });
    } catch {
      // A file that cannot be read is a file that is not there, as far as the page is concerned.
      return new Response(null, { status: NOT_FOUND });
    }
  });
}
