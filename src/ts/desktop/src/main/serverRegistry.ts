// The group servers the desktop app knows (spec of the switch to client-server, "Les serveurs enregistrés"). A user may
// have groups on several servers (question Q52), so the app always holds a list, read from a setting: the server Aspire
// started in development, or the `servers.json` file of the app's data folder once installed. The page only ever names
// a server by its identifier.

/** A server the app may forward calls to. */
export interface ServerEntry {
  /** Stable identifier, in lower case letters, digits and dashes. */
  readonly id: string;
  /** The base address of the server, such as `https://voyager.example.org`, without a trailing slash. */
  readonly url: string;
}

/** The identifier of the server Aspire starts in development. */
export const DEVELOPMENT_SERVER_ID = 'development';

const SERVER_ID = /^[a-z0-9][a-z0-9-]{0,62}$/;
const LOOPBACK_HOSTS: ReadonlySet<string> = new Set(['localhost', '127.0.0.1', '[::1]']);

/** Why the settings file was not used; the app then knows no server (and says so), rather than a wrong one. */
export class ServerSettingsError extends Error {
  public override readonly name = 'ServerSettingsError';
}

/**
 * The address of a server, checked: HTTPS, or HTTP on this machine only (a development server). Never with a user
 * name, a password, a query or a fragment. Undefined when it is none of that.
 */
export function normalizeServerUrl(text: string): string | undefined {
  const url = URL.parse(text);
  // Only the bare address of a server: no user name, password, query or fragment.
  const extras = [url?.username, url?.password, url?.search, url?.hash];
  if (url === null || extras.some((extra) => extra !== '')) {
    return undefined;
  }
  const allowed = url.protocol === 'https:' || (url.protocol === 'http:' && LOOPBACK_HOSTS.has(url.hostname));
  if (!allowed) {
    return undefined;
  }
  return `${url.origin}${url.pathname}`.replace(/\/+$/, '');
}

/**
 * Reads the servers of the settings file: `{ "servers": [ { "id": "family", "url": "https://…" } ] }`.
 * @throws ServerSettingsError when the file is not in that form, or names a server twice or with an address refused by
 * {@link normalizeServerUrl}. The message names the faulty entry by its position, never by its address.
 */
export function parseServerSettings(text: string): readonly ServerEntry[] {
  let settings: unknown;
  try {
    settings = JSON.parse(text);
  } catch {
    throw new ServerSettingsError('The server settings are not valid JSON.');
  }
  const servers = (settings as { servers?: unknown } | null)?.servers;
  if (!Array.isArray(servers)) {
    throw new ServerSettingsError('The server settings have no "servers" list.');
  }
  const entries: ServerEntry[] = [];
  servers.forEach((server: unknown, index) => {
    const { id, url } = (server ?? {}) as { id?: unknown; url?: unknown };
    if (typeof id !== 'string' || !SERVER_ID.test(id)) {
      throw new ServerSettingsError(
        `Server ${String(index + 1)} has no valid identifier (lower case letters, digits, dashes).`,
      );
    }
    if (entries.some((entry) => entry.id === id)) {
      throw new ServerSettingsError(`Server ${String(index + 1)} has the identifier of another server: ${id}.`);
    }
    const normalized = typeof url === 'string' ? normalizeServerUrl(url) : undefined;
    if (normalized === undefined) {
      throw new ServerSettingsError(
        `Server ${id} has no valid address: HTTPS, or HTTP on this machine only, without a user name or a query.`,
      );
    }
    entries.push({ id, url: normalized });
  });
  return entries;
}

/** Where the servers come from. */
export interface ServerSources {
  /** The address of the server Aspire started, in development only. */
  readonly developmentUrl?: string;
  /** Reads the settings file of the installed app; undefined when there is none. */
  readonly readSettingsFile: () => string | undefined;
}

/**
 * The servers the app knows: the development server when Aspire gives one, otherwise those of the settings file (none
 * when there is no file).
 * @throws ServerSettingsError when a setting is not valid.
 */
export function readRegisteredServers(sources: ServerSources): readonly ServerEntry[] {
  if (sources.developmentUrl !== undefined) {
    const url = normalizeServerUrl(sources.developmentUrl);
    if (url === undefined) {
      throw new ServerSettingsError('The address of the development server is not valid.');
    }
    return [{ id: DEVELOPMENT_SERVER_ID, url }];
  }
  const text = sources.readSettingsFile();
  return text === undefined ? [] : parseServerSettings(text);
}
