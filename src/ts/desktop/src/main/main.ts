// Main process of the desktop app: keeps the local service running, opens the window on the user
// interface and tells the interface where the service listens through the preload script.
import { join } from 'node:path';

import { app, BrowserWindow, ipcMain, type IpcMainInvokeEvent } from 'electron';

import {
  GET_LOCAL_SERVICE_STATE_CHANNEL,
  LOCAL_SERVICE_STATE_CHANGED_CHANNEL,
  RETRY_LOCAL_SERVICE_CHANNEL,
} from '../shared/ipc';
import { APP_ENTRY_URL, isAppUrl, registerAppScheme, serveUserInterface } from './appProtocol';
import { launchLocalService, attachToRunningLocalService, type LocalService } from './localService';
import { superviseLocalService } from './localServiceSupervisor';
import { denyAllPermissions, lockDownWebContents } from './security';

const WINDOW_WIDTH = 1024;
const WINDOW_HEIGHT = 768;
const EXECUTABLE_NAME = process.platform === 'win32' ? 'Voyager.LocalService.exe' : 'Voyager.LocalService';
// The installed app always runs its service in Production, isolated from the machine's settings:
// development ones (logs sent to Seq or to a dashboard, health endpoints) never apply to it.
const PACKAGED_SERVICE_ENVIRONMENT = { DOTNET_ENVIRONMENT: 'Production', ASPNETCORE_ENVIRONMENT: 'Production' };

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
    environment: app.isPackaged ? PACKAGED_SERVICE_ENVIRONMENT : {},
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
      console.error(message);
    },
    inform: (message) => {
      console.info(message);
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

registerAppScheme();
app.enableSandbox();

// Two instances would share, and fight over, the same local database.
if (app.requestSingleInstanceLock()) {
  start();
} else {
  app.quit();
}
