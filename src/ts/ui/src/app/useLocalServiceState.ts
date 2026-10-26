import { useEffect, useState } from 'react';

import type { LocalServiceState } from '@voyager/grpc-client';

import type { LocalServiceBridge } from './bridge';

/** Where the desktop app's local service stands, kept up to date as it restarts. */
export function useLocalServiceState(bridge: LocalServiceBridge): LocalServiceState {
  const [state, setState] = useState<LocalServiceState>({ status: 'starting' });

  useEffect(() => {
    // A change pushed while the first answer is on its way is newer than that answer.
    let pushed = false;
    let cancelled = false;
    const unsubscribe = bridge.onLocalServiceStateChange((next) => {
      pushed = true;
      setState(next);
    });
    bridge.getLocalServiceState().then(
      (current) => {
        if (!pushed && !cancelled) {
          setState(current);
        }
      },
      (error: unknown) => {
        // Shown in the developer tools; the user keeps seeing that the app is starting.
        console.error('The state of the local service is unknown:', error instanceof Error ? error.message : error);
      },
    );
    return () => {
      cancelled = true;
      unsubscribe();
    };
  }, [bridge]);

  return state;
}
