// Updates of the installed app (ADR-0009): looks for a newer version at startup and downloads it.
// It installs the next time the app starts; nothing is ever installed while the user works.

/** The part of a Velopack update the log needs: the version it brings. */
interface UpdateInfo {
  readonly TargetFullRelease: { readonly Version: string };
}

/** What the checks need from Velopack's UpdateManager: the real one fits, tests pass a stand-in. */
export interface UpdateSource<TUpdate extends UpdateInfo> {
  /** Resolves to the newer version, or null when the app is up to date. */
  readonly checkForUpdatesAsync: () => Promise<TUpdate | null>;
  readonly downloadUpdateAsync: (update: TUpdate) => Promise<void>;
}

/** Where {@link downloadUpdate} reports what it does. */
export interface UpdateLog {
  readonly info: (message: string) => void;
  readonly warn: (message: string) => void;
}

/** What {@link downloadUpdate} did: nothing to download, a version downloaded, or a failure. */
export type UpdateOutcome = 'none' | 'downloaded' | 'failed';

/**
 * Downloads the newer version, if there is one. Never throws: no network, no release published yet
 * or a download that fails must not get in the way of starting the app; a failure is logged as a
 * warning and reported as `'failed'`.
 */
export async function downloadUpdate<TUpdate extends UpdateInfo>(
  source: UpdateSource<TUpdate>,
  log: UpdateLog,
): Promise<UpdateOutcome> {
  try {
    const update = await source.checkForUpdatesAsync();
    if (update === null) {
      log.info('The app is up to date.');
      return 'none';
    }
    const version = update.TargetFullRelease.Version;
    log.info(`Version ${version} is available; downloading it.`);
    await source.downloadUpdateAsync(update);
    log.info(`Version ${version} is downloaded; it installs the next time the app starts.`);
    return 'downloaded';
  } catch (error: unknown) {
    log.warn(`The app could not check for updates: ${error instanceof Error ? error.message : String(error)}`);
    return 'failed';
  }
}
