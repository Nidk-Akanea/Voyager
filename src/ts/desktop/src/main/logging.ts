// The desktop app's own log (electron-log, ADR-0006): a file next to the local service's, in the
// folder of the user's machine, once the app is installed.
import { homedir } from 'node:os';
import { join } from 'node:path';

import log from 'electron-log/main';

import { redactLogData } from './redactHomeFolder';

const LOG_FILE_NAME = 'desktop.log';
// electron-log keeps one older file next to this one, so the log never grows past twice this.
const MAX_LOG_FILE_BYTES = 1024 * 1024;

/** The desktop app's logger; its destination is set once by {@link configureLogging}. */
export const desktopLog = log;

export interface LoggingOptions {
  /** The folder of the log file; shared with the local service. */
  readonly directory: string;
  /** Written to the file when installed; to the console in development, where the terminal shows it. */
  readonly toFile: boolean;
}

/** Sets where the app's log goes, and keeps the user's account name out of it. */
export function configureLogging({ directory, toFile }: LoggingOptions): void {
  const home = homedir();
  log.transports.file.resolvePathFn = () => join(directory, LOG_FILE_NAME);
  log.transports.file.maxSize = MAX_LOG_FILE_BYTES;
  log.transports.file.level = toFile ? 'info' : false;
  log.transports.console.level = toFile ? false : 'info';
  log.hooks.push((message) => ({
    ...message,
    data: redactLogData(message.data, home),
  }));
  // An error nobody caught still ends in the log, without a dialog nobody can act on. As with
  // Electron's own handler, the app then keeps running.
  log.errorHandler.startCatching({ showDialog: false });
}
