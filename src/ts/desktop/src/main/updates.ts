// Updates of the installed app (ADR-0014): at startup, looks for a newer version, downloads it and has
// the updater install it and start the app again, while the startup screen shows where it stands.

import { describeError } from './describeError';

const CHECK_TIMEOUT_MS = 8_000;
// A download is long, so it has no overall limit: it gives up when the progress stops moving.
const DOWNLOAD_IDLE_TIMEOUT_MS = 60_000;
const FULL_PERCENT = 100;

/** The part of a Velopack update the app needs: the version it brings. */
interface UpdateInfo {
  readonly TargetFullRelease: { readonly Version: string };
}

/** What the update needs from Velopack's UpdateManager: the real one fits, tests pass a stand-in. */
export interface UpdateClient<TUpdate extends UpdateInfo> {
  /** Resolves to the newer version, or null when the app is up to date. */
  readonly checkForUpdatesAsync: () => Promise<TUpdate | null>;
  /** Downloads the update, reporting a percentage as it goes. */
  readonly downloadUpdateAsync: (update: TUpdate, progress?: (percent: number) => void) => Promise<void>;
  /** Has the updater wait for this app to exit, install the update and start the app again. */
  readonly waitExitThenApplyUpdate: (update: TUpdate, silent?: boolean, restart?: boolean) => void;
}

/** Where {@link updateApp} reports what it does in the log. */
export interface UpdateLog {
  readonly info: (message: string) => void;
  readonly warn: (message: string) => void;
}

/** What {@link updateApp} is doing, for the startup screen. */
export type UpdateEvent =
  | { readonly kind: 'checking' }
  | { readonly kind: 'downloading'; readonly version: string; readonly percent: number }
  | { readonly kind: 'installing'; readonly version: string };

/**
 * What {@link updateApp} did: nothing to install, an update handed to the updater (the app must quit
 * now for it to be installed), or a failure.
 */
export type UpdateOutcome = 'up-to-date' | 'restarting' | 'failed';

/** The patience of {@link updateApp}; each limit has a default, tests shorten them. */
export interface UpdateTimeouts {
  /** How long the search for a newer version may take. */
  readonly checkMs?: number;
  /** How long a download may go without its percentage moving. */
  readonly downloadIdleMs?: number;
}

/**
 * Looks for a newer version and, if there is one, downloads it and hands it to the updater. Never
 * throws: no network, no release published yet or a download that fails must not get in the way of
 * starting the app; a failure is logged as a warning and reported as `'failed'`.
 */
export async function updateApp<TUpdate extends UpdateInfo>(
  client: UpdateClient<TUpdate>,
  log: UpdateLog,
  report: (event: UpdateEvent) => void,
  { checkMs = CHECK_TIMEOUT_MS, downloadIdleMs = DOWNLOAD_IDLE_TIMEOUT_MS }: UpdateTimeouts = {},
): Promise<UpdateOutcome> {
  try {
    report({ kind: 'checking' });
    const update = await withTimeout(client.checkForUpdatesAsync(), checkMs, 'The update server did not answer');
    if (update === null) {
      log.info('The app is up to date.');
      return 'up-to-date';
    }

    const version = update.TargetFullRelease.Version;
    log.info(`Version ${version} is available; downloading it.`);
    await download(client, update, downloadIdleMs, (percent) => {
      report({ kind: 'downloading', version, percent });
    });

    log.info(`Version ${version} is downloaded; the app restarts to install it.`);
    report({ kind: 'installing', version });
    client.waitExitThenApplyUpdate(update, true, true);
    return 'restarting';
  } catch (error: unknown) {
    log.warn(`The app could not update: ${describeError(error)}`);
    return 'failed';
  }
}

/** Downloads an update, telling each new percentage once, and not at all once the download is over. */
async function download<TUpdate extends UpdateInfo>(
  client: UpdateClient<TUpdate>,
  update: TUpdate,
  idleMs: number,
  onPercent: (percent: number) => void,
): Promise<void> {
  let finished = false;
  let lastPercent = -1;
  let idleTimer: NodeJS.Timeout | undefined;
  let giveUp: (error: Error) => void = () => undefined;
  const stalled = new Promise<never>((_resolve, reject) => {
    giveUp = reject;
  });

  const armIdleTimer = (): void => {
    clearTimeout(idleTimer);
    idleTimer = setTimeout(() => {
      giveUp(new Error(`The download made no progress for ${String(idleMs / 1000)} seconds.`));
    }, idleMs);
  };
  const progress = (percent: number): void => {
    // The percentages come from native code: one may arrive after the download is over.
    if (finished) {
      return;
    }
    armIdleTimer();
    const rounded = clampPercent(percent);
    if (rounded !== lastPercent) {
      lastPercent = rounded;
      onPercent(rounded);
    }
  };

  armIdleTimer();
  progress(0);
  try {
    await Promise.race([client.downloadUpdateAsync(update, progress), stalled]);
  } finally {
    finished = true;
    clearTimeout(idleTimer);
  }
}

/** A percentage as a whole number between 0 and 100: what the startup screen shows. */
function clampPercent(percent: number): number {
  return Math.min(FULL_PERCENT, Math.max(0, Math.round(percent)));
}

/** Rejects when `promise` takes longer than `timeoutMs`: a server that does not answer must not hold up the app. */
async function withTimeout<T>(promise: Promise<T>, timeoutMs: number, message: string): Promise<T> {
  let timer: NodeJS.Timeout | undefined;
  const timeout = new Promise<never>((_resolve, reject) => {
    timer = setTimeout(() => {
      reject(new Error(`${message} within ${String(timeoutMs / 1000)} seconds.`));
    }, timeoutMs);
  });
  try {
    return await Promise.race([promise, timeout]);
  } finally {
    clearTimeout(timer);
  }
}
