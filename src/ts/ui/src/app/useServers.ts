// The group servers the desktop app knows, and its own version, as the main process tells them.
import { useEffect, useState } from 'react';

import type { RegisteredServer } from '@voyager/grpc-client';

import type { ServerBridge } from './bridge';

export interface KnownServers {
  readonly servers: readonly RegisteredServer[];
  /** The version of the app; undefined when the main process could not tell. */
  readonly appVersion?: string;
}

/** Undefined until the main process answers; no server when it cannot. */
export function useServers(bridge: ServerBridge): KnownServers | undefined {
  const [known, setKnown] = useState<KnownServers | undefined>();
  useEffect(() => {
    let cancelled = false;
    Promise.all([bridge.getServers(), bridge.getAppVersion().catch(() => undefined)]).then(
      ([servers, appVersion]) => {
        if (!cancelled) {
          setKnown({ servers, appVersion });
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
