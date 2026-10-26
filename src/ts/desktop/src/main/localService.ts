// Lifecycle of the local .NET service (ADR-0003): starts it with a fresh token on a loopback port of
// its own choosing, waits for the address it announces, and stops it cleanly with the app.
import { spawn } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { dirname } from 'node:path';
import { createInterface } from 'node:readline';
import { setTimeout as delay } from 'node:timers/promises';

import type { LocalServiceConnection } from '@voyager/grpc-client';

const TOKEN_BYTES = 32;
const STARTUP_TIMEOUT_MS = 30_000;
const STOP_TIMEOUT_MS = 5_000;
// The service picks a free port itself, so no other program can take it in between.
const ANY_LOOPBACK_PORT = 'http://127.0.0.1:0';
// Must match DesktopAppLink.LISTENING_PREFIX in src/dotnet/Voyager.LocalService/DesktopAppLink.cs.
const LISTENING_PREFIX = 'VOYAGER_LISTENING ';

// What an isolated service still inherits from the machine: system locations and runtime basics only.
// Every other variable could configure it, since ASP.NET Core reads them all (Voyager__…, Kestrel__…,
// urls, Seq__…, OTEL_…, ASPNETCORE_ENVIRONMENT…). Names are compared without case, as Windows does.
const SYSTEM_VARIABLES = new Set([
  'APPDATA',
  'COMMONPROGRAMFILES',
  'COMMONPROGRAMFILES(X86)',
  'COMPUTERNAME',
  'DOTNET_ROOT',
  'DOTNET_ROOT(X86)',
  'HOME',
  'HOMEDRIVE',
  'HOMEPATH',
  'LANG',
  'LC_ALL',
  'LOCALAPPDATA',
  'NUMBER_OF_PROCESSORS',
  'OS',
  'PATH',
  'PATHEXT',
  'PROCESSOR_ARCHITECTURE',
  'PROGRAMDATA',
  'PROGRAMFILES',
  'PROGRAMFILES(X86)',
  'PROGRAMW6432',
  'SYSTEMDRIVE',
  'SYSTEMROOT',
  'TEMP',
  'TMP',
  'TZ',
  'USERDOMAIN',
  'USERNAME',
  'USERPROFILE',
  'WINDIR',
]);

export interface LocalService {
  /** Resolves once the service accepts calls; rejects if it cannot start. */
  readonly connection: Promise<LocalServiceConnection>;
  /** Resolves when the service process is gone, whether it was stopped or died; never for a service started by someone else. */
  readonly exited: Promise<void>;
  /** Asks the service to stop, kills it if it has not stopped in time, and resolves once it is gone. */
  readonly stop: () => Promise<void>;
}

export interface LaunchOptions {
  /** The local service executable. */
  readonly executable: string;
  /** Arguments of the executable; the tests use them to run a stand-in for the service. */
  readonly args?: readonly string[];
  /** How long to wait for the service to announce its address. */
  readonly startupTimeoutMs?: number;
  /** Receives each line the service writes on its error output; defaults to the console. */
  readonly onOutput?: (line: string) => void;
  /** Variables added to, or replacing, those the app inherited. */
  readonly environment?: Readonly<Record<string, string>>;
  /**
   * Passes on only the system variables of the machine, none that could configure the service: the
   * installed app alone decides how its service runs.
   */
  readonly isolated?: boolean;
}

/** A service started, and stopped, by someone else: Aspire in development. */
export function attachToRunningLocalService(connection: LocalServiceConnection): LocalService {
  return {
    connection: Promise.resolve(connection),
    exited: new Promise<void>(() => undefined),
    stop: () => Promise.resolve(),
  };
}

/** Starts the local service as a child process of the app. */
export function launchLocalService({
  executable,
  args = [],
  startupTimeoutMs = STARTUP_TIMEOUT_MS,
  onOutput = (line) => {
    console.error('[local service]', line);
  },
  environment = {},
  isolated = false,
}: LaunchOptions): LocalService {
  const token = randomBytes(TOKEN_BYTES).toString('base64url');
  const inherited = isolated
    ? Object.fromEntries(Object.entries(process.env).filter(([name]) => SYSTEM_VARIABLES.has(name.toUpperCase())))
    : process.env;
  const child = spawn(executable, args, {
    env: {
      ...inherited,
      ...environment,
      ASPNETCORE_URLS: ANY_LOOPBACK_PORT,
      Voyager__LocalToken: token,
      // The open standard input ties the service to the app: it stops when the app quits or crashes.
      Voyager__StopWhenInputCloses: 'true',
    },
    // ASP.NET Core looks for appsettings.json in the working directory, not next to the executable.
    cwd: dirname(executable),
    stdio: ['pipe', 'pipe', 'pipe'],
    windowsHide: true,
  });
  // Writing to a service that already stopped fails; there is nothing more to do then.
  child.stdin.on('error', () => undefined);
  createInterface({ input: child.stderr }).on('line', onOutput);

  const gone = new Promise<void>((resolve) => {
    child.once('exit', () => {
      resolve();
    });
    child.once('error', () => {
      resolve();
    });
  });

  const connection = new Promise<LocalServiceConnection>((resolve, reject) => {
    const timer = setTimeout(() => {
      reject(new Error(`The local service did not start within ${String(startupTimeoutMs / 1000)} seconds.`));
      child.kill();
    }, startupTimeoutMs);
    createInterface({ input: child.stdout }).on('line', (line) => {
      if (line.startsWith(LISTENING_PREFIX)) {
        clearTimeout(timer);
        resolve({ url: line.slice(LISTENING_PREFIX.length).trim(), token });
      }
    });
    child.once('error', (error) => {
      clearTimeout(timer);
      reject(error);
    });
    // 'close' rather than 'exit': it comes once the output has been read, announcement included.
    child.once('close', (code) => {
      clearTimeout(timer);
      reject(new Error(`The local service stopped during startup (exit code ${String(code)}).`));
    });
  });

  let stopping: Promise<void> | undefined;
  const stop = (): Promise<void> => {
    stopping ??= (async () => {
      child.stdin.end();
      const deadline = delay(STOP_TIMEOUT_MS, 'timeout' as const, { ref: false });
      if ((await Promise.race([gone, deadline])) === 'timeout') {
        child.kill();
      }
      await gone;
    })();
    return stopping;
  };

  return { connection, exited: gone, stop };
}
