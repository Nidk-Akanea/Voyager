// The group servers the desktop app knows, the one this view shows, and the app's own version, as the main process
// tells them.
import { useEffect, useState } from 'react';

import type { RegisteredServer, ServerChoice } from '@voyager/grpc-client';

import type { ServerBridge } from './bridge';

export interface KnownServers {
  /** Every server the app knows, by identifier only (for the selector). */
  readonly servers: readonly ServerChoice[];
  /** The server of this view, with its address; undefined when the app knows none. */
  readonly current?: RegisteredServer;
  /** The version of the app; undefined when the main process could not tell. */
  readonly appVersion?: string;
}

/** Undefined until the main process answers; no server when it cannot. */
export function useServers(bridge: ServerBridge): KnownServers | undefined {
  const [known, setKnown] = useState<KnownServers | undefined>();
  useEffect(() => {
    let cancelled = false;
    Promise.all([bridge.getServers(), bridge.getCurrentServer(), bridge.getAppVersion().catch(() => undefined)]).then(
      ([servers, current, appVersion]) => {
        if (!cancelled) {
          setKnown({ servers, current, appVersion });
        }
      },
      (error: unknown) => {
        console.error('The servers of the app are unknown:', error instanceof Error ? error.message : error);
        if (!cancelled) {
          setKnown({ servers: [] });
        }
      },
    );
    return () => {
      cancelled = true;
    };
  }, [bridge]);
  return known;
}
