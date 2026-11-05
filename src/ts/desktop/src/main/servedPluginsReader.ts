// Where the interface of each plugin of a server is, for the view of that server: read as the view opens, then again
// when the page asks for a plugin version not served yet (the server restarted with another one, or a download failed).
import type { PluginAddress } from './pluginFiles';
import { servedPluginKey, type ServedPlugins } from './servedPlugins';

export interface ServedPluginsReaderOptions {
  /** Lists the plugins of the server and makes their interfaces ready; undefined when the server could not tell. */
  readonly read: () => Promise<ServedPlugins | undefined>;
  /** How long after a reading the page may make the reader ask the server again. */
  readonly retryAfterMs: number;
  readonly now?: () => number;
}

export interface ServedPluginsReader {
  /** Reads now, unless a reading is under way: the view opens, so its page will soon ask. */
  readonly refresh: () => void;
  /**
   * The plugins to serve, for a page that asks for one: at once when it is served, else after a new reading, never more
   * than one at a time, nor sooner than `retryAfterMs` after the last.
   */
  readonly find: (wanted: PluginAddress) => Promise<ServedPlugins>;
}

export function createServedPluginsReader(options: ServedPluginsReaderOptions): ServedPluginsReader {
  const now = options.now ?? Date.now;
  let served: ServedPlugins = new Map<string, string>();
  let reading: Promise<void> | undefined;
  let lastReading = Number.NEGATIVE_INFINITY;

  const startReading = (): Promise<void> => {
    reading ??= options
      .read()
      .then((read) => {
        // A server that could not tell keeps what it served before.
        if (read !== undefined) {
          served = read;
        }
      })
      .finally(() => {
        lastReading = now();
        reading = undefined;
      });
    return reading;
  };

  return {
    refresh: () => {
      void startReading();
    },
    find: async (wanted) => {
      if (served.has(servedPluginKey(wanted.origin, wanted.pluginId, wanted.version))) {
        return served;
      }
      if (reading !== undefined || now() - lastReading >= options.retryAfterMs) {
        await startReading();
      }
      return served;
    },
  };
}
