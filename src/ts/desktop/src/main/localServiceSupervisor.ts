// Keeps the local service running while the app does (docs/schemas/technique.md): starts it, starts it
// again when it dies, and gives up with an explicit "failed" state the user can retry from.
import type { LocalServiceState } from '@voyager/grpc-client';

import { describeError } from './describeError';
import type { LocalService } from './localService';

// One entry per automatic restart allowed within the window: the wait before each of them.
const RESTART_DELAYS_MS: readonly number[] = [1_000, 2_000, 4_000];
const RESTART_WINDOW_MS = 60_000;

export interface SupervisorOptions {
  /** Starts the service; called again for every restart. */
  readonly launch: () => LocalService;
  /** Where the failures and abnormal events go (a death, a failed start); never the user interface. */
  readonly report?: (message: string) => void;
  /** Where the notable steps of a normal life go (service ready, retry requested); never the user interface. */
  readonly inform?: (message: string) => void;
  /** Waits before each automatic restart; its length is the number of restarts allowed per window. */
  readonly restartDelaysMs?: readonly number[];
  /** Deaths older than this are forgotten: a service that ran for a while gets its restarts back. */
  readonly restartWindowMs?: number;
  /** The clock, in milliseconds; tests replace it to move through the restart window. */
  readonly now?: () => number;
}

export interface LocalServiceSupervisor {
  /** The current state; the user interface is told of every later change through {@link onChange}. */
  readonly state: () => LocalServiceState;
  /** Calls the listener at every change of state; returns the function that removes it. */
  readonly onChange: (listener: (state: LocalServiceState) => void) => () => void;
  /**
   * Resolves once the service is ready or has failed (no longer on its way), or once it is stopped: the
   * app never waits for a service that will not come.
   */
  readonly whenSettled: () => Promise<void>;
  /** Starts the service again after a failure; does nothing in any other state. */
  readonly retry: () => void;
  /** Stops the service for good: nothing is restarted afterwards. */
  readonly stop: () => Promise<void>;
}

/** Starts the local service and keeps it running; see {@link SupervisorOptions}. */
export function superviseLocalService({
  launch,
  report = () => undefined,
  inform = () => undefined,
  restartDelaysMs = RESTART_DELAYS_MS,
  restartWindowMs = RESTART_WINDOW_MS,
  now = Date.now,
}: SupervisorOptions): LocalServiceSupervisor {
  const listeners = new Set<(state: LocalServiceState) => void>();
  let current: LocalServiceState = { status: 'starting' };
  let service: LocalService | undefined;
  let restartTimer: NodeJS.Timeout | undefined;
  let stopped = false;
  // Tells the launch whose news still counts: a previous one may answer after being replaced.
  let generation = 0;
  let deaths: number[] = [];
  let markSettled: () => void = () => undefined;
  const settled = new Promise<void>((resolve) => {
    markSettled = resolve;
  });

  const update = (next: LocalServiceState): void => {
    if (next.status === current.status && next.status !== 'ready') {
      return;
    }
    current = next;
    if (next.status === 'ready' || next.status === 'failed') {
      markSettled();
    }
    for (const listener of listeners) {
      // A listener that fails (a window being destroyed) must not stop the supervision.
      try {
        listener(next);
      } catch (error: unknown) {
        report(`A listener of the local service state failed: ${describeError(error)}`);
      }
    }
  };

  const fail = (reason: string, launched: LocalService): void => {
    report(reason);
    // The process may still be alive (startup timeout): never leave one behind.
    void launched.stop();
    update({ status: 'failed' });
  };

  const start = (status: 'starting' | 'restarting'): void => {
    const id = ++generation;
    update({ status });
    let launched: LocalService;
    try {
      launched = launch();
    } catch (error: unknown) {
      report(`The local service could not be launched: ${describeError(error)}`);
      update({ status: 'failed' });
      return;
    }
    service = launched;

    launched.connection.then(
      (connection) => {
        if (id !== generation) {
          return;
        }
        void launched.exited.then(() => {
          if (id === generation) {
            handleDeath(launched);
          }
        });
        inform('The local service is ready.');
        update({ status: 'ready', connection });
      },
      (error: unknown) => {
        if (id === generation) {
          fail(`The local service could not start: ${describeError(error)}`, launched);
        }
      },
    );
  };

  const handleDeath = (dead: LocalService): void => {
    const at = now();
    deaths = [...deaths.filter((death) => at - death < restartWindowMs), at];
    const delay = restartDelaysMs[deaths.length - 1];
    if (delay === undefined) {
      fail(
        `The local service stopped ${String(deaths.length)} times within ${String(restartWindowMs / 1000)} s; not restarting it.`,
        dead,
      );
      return;
    }
    report(`The local service stopped unexpectedly; restarting it in ${String(delay)} ms.`);
    update({ status: 'restarting' });
    restartTimer = setTimeout(() => {
      restartTimer = undefined;
      if (!stopped) {
        start('restarting');
      }
    }, delay);
  };

  start('starting');

  return {
    state: () => current,
    onChange: (listener) => {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    whenSettled: () => settled,
    retry: () => {
      if (stopped || current.status !== 'failed') {
        return;
      }
      inform('Starting the local service again after a retry request.');
      deaths = [];
      start('starting');
    },
    stop: async () => {
      stopped = true;
      markSettled();
      generation++;
      clearTimeout(restartTimer);
      listeners.clear();
      await service?.stop();
    },
  };
}
