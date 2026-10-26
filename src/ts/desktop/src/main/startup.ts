// What the app does between being launched and opening its window (ADR-0014): looks for an update,
// which restarts the app into the new version, or starts the local service and opens the window.
import type { StartupState } from '@voyager/grpc-client';

import type { UpdateEvent, UpdateOutcome } from './updates';

/** The steps of the startup, given by the main process so that their order can be tested on its own. */
export interface StartupSteps {
  /** Looks for an update and, if there is one, hands it to the updater. */
  readonly updateApp: (report: (event: UpdateEvent) => void) => Promise<UpdateOutcome>;
  /** Starts the local service; resolves once it is ready or has failed. */
  readonly startLocalService: () => Promise<void>;
  /** Tells the startup screen where the app stands. */
  readonly report: (state: StartupState) => void;
  /** Opens the main window; the startup window stays until it is on screen. */
  readonly openMainWindow: () => void;
}

/** `restarting-for-update`: the app must quit now, the updater starts the new version. */
export type StartupResult = 'opened' | 'restarting-for-update';

/** What the startup screen shows for a step of the update. */
export function toStartupState(event: UpdateEvent): StartupState {
  switch (event.kind) {
    case 'checking':
      return { status: 'checking-updates' };
    case 'downloading':
      return { status: 'downloading-update', version: event.version, percent: event.percent };
    case 'installing':
      return { status: 'installing-update', version: event.version };
  }
}

/**
 * The local service starts only once no update is on its way: it would hold files of the folder the
 * updater is about to replace. A failed update, or none, does not stop the app from opening.
 */
export async function runStartup(steps: StartupSteps): Promise<StartupResult> {
  const outcome = await steps.updateApp((event) => {
    steps.report(toStartupState(event));
  });
  if (outcome === 'restarting') {
    return 'restarting-for-update';
  }

  steps.report({ status: 'starting-service' });
  await steps.startLocalService();
  steps.openMainWindow();
  return 'opened';
}
