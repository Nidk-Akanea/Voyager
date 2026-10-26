// Main process of the desktop app: keeps the local service running, opens the window on the user
// interface and tells the interface where the service listens through the preload script.
import { join } from 'node:path';

import { app, BrowserWindow, ipcMain, type IpcMainInvokeEvent } from 'electron';
import { GithubSource, UpdateManager, VelopackApp } from 'velopack';

import {
  GET_LOCAL_SERVICE_STATE_CHANNEL,
  LOCAL_SERVICE_STATE_CHANGED_CHANNEL,
  RETRY_LOCAL_SERVICE_CHANNEL,
} from '../shared/ipc';
import { APP_ENTRY_URL, isAppUrl, registerAppScheme, serveUserInterface } from './appProtocol';
import { launchLocalService, attachToRunningLocalService, type LocalService } from './localService';
import { superviseLocalService } from './localServiceSupervisor';
import { configureLogging, desktopLog } from './logging';
import { denyAllPermissions, lockDownWebContents } from './security';
import { downloadUpdate } from './updates';

const WINDOW_WIDTH = 1024;
const WINDOW_HEIGHT = 768;
const EXECUTABLE_NAME = process.platform === 'win32' ? 'Voyager.LocalService.exe' : 'Voyager.LocalService';
// Updates come from the releases of the repository (ADR-0009).
const RELEASES_REPOSITORY_URL = 'https://github.com/Nidk-Akanea/Voyager';

/** The folder of the log files, for the app and its local service: next to the local database. */
function logDirectory(): string {
  return join(process.env.LOCALAPPDATA ?? app.getPath('appData'), 'Voyager', 'logs');
}

// The installed app always runs its service in Production, isolated from the machine's settings:
// development ones (logs sent to Seq or to a dashboard, health endpoints) never apply to it.
function packagedServiceEnvironment(): Record<string, string> {
  return {
    DOTNET_ENVIRONMENT: 'Production',
    ASPNETCORE_ENVIRONMENT: 'Production',
    Voyager__LogDirectory: logDirectory(),
  };
}

// Only the loopback interface: the page talks to the local service and nothing else.
const CONTENT_SECURITY_POLICY = [
  "default-src 'self'",
  "script-src 'self'",
  // Fluent UI (Griffel) inserts its styles while the page runs.
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data:",
  'connect-src http://127.0.0.1:*',
  "object-src 'none'",
  "base-uri 'none'",
  "form-action 'none'",
  "frame-ancestors 'none'",
].join('; ');

/**
 * A setting Aspire passes in development. Ignored once packaged, so that nothing in a user's
 * environment can point the app at another page or another service.
 */
function developmentSetting(name: string): string | undefined {
  const value = app.isPackaged ? undefined : process.env[name];
  return value === '' ? undefined : value;
}

// In development, Aspire serves the user interface with Vite and passes its address.
const DEV_USER_INTERFACE_URL = developmentSetting('VOYAGER_UI_URL');

/** Whether a page is the app's user interface: the only page allowed to load and to call the main process. */
function isUserInterface(url: string): boolean {
  return DEV_USER_INTERFACE_URL === undefined
    ? isAppUrl(url)
    : URL.parse(url)?.origin === new URL(DEV_USER_INTERFACE_URL).origin;
}

/** The built user interface: shipped with the app, or built next to this package in the repository. */
function userInterfaceDirectory(): string {
  return app.isPackaged ? join(process.resourcesPath, 'ui') : join(import.meta.dirname, '..', '..', 'ui', 'dist');
}

/** The service Aspire started in development, or else the executable shipped with the app. */
function startLocalService(): LocalService {
  const url = developmentSetting('VOYAGER_LOCAL_SERVICE_URL');
  const token = developmentSetting('VOYAGER_LOCAL_TOKEN');
  if (url !== undefined && token !== undefined) {
    return attachToRunningLocalService({ url, token });
  }
  return launchLocalService({
    executable:
      developmentSetting('VOYAGER_LOCAL_SERVICE_PATH') ?? join(process.resourcesPath, 'local-service', EXECUTABLE_NAME),
    onOutput: (line) => {
      desktopLog.warn('[local service]', line);
    },
    environment: app.isPackaged ? packagedServiceEnvironment() : {},
    isolated: app.isPackaged,
  });
}

function createWindow(): BrowserWindow {
  const window = new BrowserWindow({
    width: WINDOW_WIDTH,
    height: WINDOW_HEIGHT,
    show: false,
    webPreferences: {
      preload: join(import.meta.dirname, 'preload.cjs'),
      contextIsolation: true,
      sandbox: true,
      nodeIntegration: false,
      webSecurity: true,
      spellcheck: false,
    },
  });
  window.once('ready-to-show', () => {
    window.show();
  });
  void window.loadURL(DEV_USER_INTERFACE_URL ?? APP_ENTRY_URL);
  return window;
}

function start(): void {
  const localService = superviseLocalService({
    launch: startLocalService,
    report: (message) => {
      desktopLog.error(message);
    },
    inform: (message) => {
      desktopLog.info(message);
    },
  });
  localService.onChange((state) => {
    // Only the user interface gets to know where the service listens, and its token.
    for (const window of BrowserWindow.getAllWindows()) {
      if (!window.webContents.isDestroyed() && isUserInterface(window.webContents.getURL())) {
        window.webContents.send(LOCAL_SERVICE_STATE_CHANGED_CHANNEL, state);
      }
    }
  });

  // Quitting waits for the local service to stop, so that it never writes to a database left behind.
  let localServiceStopped = false;
  app.on('will-quit', (event) => {
    if (localServiceStopped) {
      return;
    }
    event.preventDefault();
    void localService.stop().finally(() => {
      localServiceStopped = true;
      app.quit();
    });
  });

  const checkCaller = (event: IpcMainInvokeEvent): void => {
    if (!isUserInterface(event.senderFrame?.url ?? '')) {
      throw new Error('Only the Voyager user interface can use the local service.');
    }
  };
  ipcMain.handle(GET_LOCAL_SERVICE_STATE_CHANNEL, (event) => {
    checkCaller(event);
    return localService.state();
  });
  ipcMain.handle(RETRY_LOCAL_SERVICE_CHANNEL, (event) => {
    checkCaller(event);
    localService.retry();
  });

  lockDownWebContents(isUserInterface);
  app.on('window-all-closed', () => {
    app.quit();
  });

  void app.whenReady().then(() => {
    if (app.isPackaged) {
      void updateInstalledApp();
    }
    denyAllPermissions();
    serveUserInterface(userInterfaceDirectory(), CONTENT_SECURITY_POLICY);
    const window = createWindow();

    // Opening Voyager again brings back the window already open.
    app.on('second-instance', () => {
      if (window.isMinimized()) {
        window.restore();
      }
      window.focus();
    });
  });
}

/** Looks for a newer version of the installed app; an app run from a plain folder has nothing to update. */
async function updateInstalledApp(): Promise<void> {
  let updates: UpdateManager;
  try {
    // A version with a suffix (0.3.0-beta.2) comes from the beta channel: its releases are pre-releases.
    const prerelease = app.getVersion().includes('-');
    updates = new UpdateManager(new GithubSource(RELEASES_REPOSITORY_URL, undefined, prerelease));
  } catch (error: unknown) {
    // Not installed by its installer (run from a plain folder): there is nothing to update.
    desktopLog.info(
      'Updates are off: the app is not installed by its installer.',
      error instanceof Error ? error.message : String(error),
    );
    return;
  }
  await downloadUpdate(updates, desktopLog);
}

configureLogging({ directory: logDirectory(), toFile: app.isPackaged });
if (app.isPackaged) {
  // Before anything else: the installer and the updater start the app to run their own steps.
  VelopackApp.build()
    // A version downloaded earlier is installed here, as the app starts (this is Velopack's default).
    .setAutoApplyOnStartup(true)
    .setLogger((level, message) => {
      // Velopack's own levels are kept: its debug and trace lines stay out of the file, which starts at info.
      desktopLog[level === 'trace' ? 'debug' : level]('[velopack]', message);
    })
    .run();
}
registerAppScheme();
app.enableSandbox();

// Two instances would share, and fight over, the same local database.
if (app.requestSingleInstanceLock()) {
  start();
} else {
  app.quit();
}
