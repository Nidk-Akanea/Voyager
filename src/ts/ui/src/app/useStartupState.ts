import { useEffect, useState } from 'react';

import type { StartupState } from '@voyager/grpc-client';

import type { StartupBridge } from './bridge';

/** What the desktop app is doing before its window opens, kept up to date as it goes. */
export function useStartupState(bridge: StartupBridge): StartupState {
  const [state, setState] = useState<StartupState>({ status: 'checking-updates' });

  useEffect(() => {
    // A change pushed while the first answer is on its way is newer than that answer.
    let pushed = false;
    let cancelled = false;
    const unsubscribe = bridge.onStartupStateChange((next) => {
      pushed = true;
      setState(next);
    });
    bridge.getStartupState().then(
      (current) => {
        if (!pushed && !cancelled) {
          setState(current);
        }
      },
      (error: unknown) => {
        // Shown in the developer tools; the screen keeps showing the last state it knows.
        console.error('The startup state is unknown:', error instanceof Error ? error.message : error);
      },
    );
    return () => {
      cancelled = true;
      unsubscribe();
    };
  }, [bridge]);

  return state;
}
