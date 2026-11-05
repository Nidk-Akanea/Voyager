// Main process of the desktop app: looks for an update while a small startup window is shown (ADR-0014), opens the
// main window on the user interface, forwards the calls of the page to its group servers (ADR-0018, ADR-0019), and
// serves the interface modules of their plugins (ADR-0020, decision 8). The local service still runs, called by nothing,
// until lot 3 of the switch to client-server removes it.
import { existsSync, readFileSync } from 'node:fs';
import { readdir, rm } from 'node:fs/promises';
import { join } from 'node:path';

import {
  app,
  BaseWindow,
  BrowserWindow,
  ipcMain,
  Menu,
  net,
  protocol,
  session,
  WebContentsView,
  type IpcMainInvokeEvent,
} from 'electron';
import { GithubSource, HttpSource, UpdateManager, VelopackApp } from 'velopack';

import {
  Code,
  ConnectError,
  createServerClientsAt,
  type RegisteredServer,
  type ServerChoice,
  type StartupState,
} from '@voyager/grpc-client';

import {
  GET_APP_VERSION_CHANNEL,
  GET_CURRENT_SERVER_CHANNEL,
  GET_SERVERS_CHANNEL,
  GET_STARTUP_STATE_CHANNEL,
  SELECT_SERVER_CHANNEL,
  STARTUP_STATE_CHANGED_CHANNEL,
} from '../shared/ipc';
import { APP_ENTRY_URL, isAppUrl, registerAppScheme, serveUserInterface } from './appProtocol';
import { boundedFetch } from './boundedFetch';
import { buildContentSecurityPolicy, findImportMapSource } from './contentSecurityPolicy';
import { describeError } from './describeError';
import { MAXIMUM_FILE_BYTES, prepareInterfaceModules } from './interfaceModules';
import { launchLocalService, attachToRunningLocalService, type LocalService } from './localService';
import { superviseLocalService, type LocalServiceSupervisor } from './localServiceSupervisor';
import { configureLogging, desktopLog } from './logging';
import { denyAllPermissions, lockDownWebContents } from './security';
import type { ServedPlugins } from './servedPlugins';
import { createServedPluginsReader, type ServedPluginsReader } from './servedPluginsReader';
import { handleServerApiRequest, type CallingApp } from './serverApi';
import { readRegisteredServers, ServerSettingsError, type ServerEntry } from './serverRegistry';
import { serverPartition, ServerViews, type ServerView } from './serverViews';
import { runStartup } from './startup';
import { chooseUpdateSource, hideAddress, UPDATE_SOURCE_SETTING } from './updateSource';
import { updateApp, type UpdateEvent, type UpdateOutcome } from './updates';

const WINDOW_WIDTH = 1024;
const WINDOW_HEIGHT = 768;
const STARTUP_WINDOW_WIDTH = 460;
const STARTUP_WINDOW_HEIGHT = 300;
// Read by the user interface (src/ts/ui/src/main.tsx) to draw the startup screen in this window.
const STARTUP_SCREEN_QUERY = '?screen=startup';
const EXECUTABLE_NAME = process.platform === 'win32' ? 'Voyager.LocalService.exe' : 'Voyager.LocalService';
// Updates come from the releases of the repository, unless the package was built for another server
// (ADR-0014).
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
    ...pluginsEnvironment(),
  };
}

// Given by the app and never read from the machine once packaged: loading assemblies from a folder the user can
// choose is a risk that ADR-0015 declines.
function pluginsEnvironment(): Record<string, string> {
  return {
    Voyager__ShippedPluginsDirectory: shippedPluginsDirectory(),
    Voyager__UserPluginsDirectory: userPluginsDirectory(),
  };
}

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

/** Whether a page is the app's user interface: the only one allowed to load and to call the main process. */
function isUserInterface(url: string): boolean {
  return DEV_USER_INTERFACE_URL === undefined
    ? isAppUrl(url)
    : URL.parse(url)?.origin === new URL(DEV_USER_INTERFACE_URL).origin;
}

/** The built user interface: shipped with the app, or built next to this package in the repository. */
function userInterfaceDirectory(): string {
  return app.isPackaged ? join(process.resourcesPath, 'ui') : join(import.meta.dirname, '..', '..', 'ui', 'dist');
}

// A folder of artifacts in the repository, from the build of this package (src/ts/desktop/dist).
function repositoryArtifacts(folder: string): string {
  return join(import.meta.dirname, '..', '..', '..', '..', 'artifacts', folder);
}

/**
 * The plugins shipped with the app, or built next to this package in the repository (pwsh tools/build-plugins.ps1).
 * Never read from the machine's environment once packaged.
 */
function shippedPluginsDirectory(): string {
  return app.isPackaged
    ? join(process.resourcesPath, 'plugins')
    : (developmentSetting('VOYAGER_PLUGINS_DIRECTORY') ?? repositoryArtifacts('plugins'));
}

/**
 * The plugins folder of the user's data (ADR-0016): installed plugins, requests and state, next to the local
 * database. In development, under artifacts, never the folder of the installed app, which the developer would
 * otherwise share with it.
 */
function userPluginsDirectory(): string {
  return app.isPackaged
    ? join(process.env.LOCALAPPDATA ?? app.getPath('appData'), 'Voyager', 'plugins')
    : (developmentSetting('VOYAGER_USER_PLUGINS_DIRECTORY') ?? repositoryArtifacts('user-plugins'));
}

/**
 * The data of each group server on this machine, one folder per identifier: `<id>/plugins` holds the interface modules
 * downloaded from it (ADR-0018, decision 5). In development, under artifacts.
 */
function serversDataDirectory(): string {
  return app.isPackaged
    ? join(process.env.LOCALAPPDATA ?? app.getPath('appData'), 'Voyager', 'servers')
    : repositoryArtifacts('servers');
}

// How long the main process waits for the list of plugins, and for each file of an interface: the page waits for them.
const LIST_PLUGINS_TIMEOUT_MS = 10_000;
const INTERFACE_FILE_TIMEOUT_MS = 30_000;
// How long after a reading of the list of plugins the file of a plugin version not served yet asks the server again.
const SERVED_PLUGINS_RETRY_MS = 5_000;
// The largest answer the main process reads from a server: a file of an interface, with room for the gRPC envelope.
const MAXIMUM_RESPONSE_BYTES = MAXIMUM_FILE_BYTES + 64 * 1024;

/** Who calls the servers: the desktop app and its version (docs/conventions/grpc.md). */
function callingApp(): CallingApp {
  return { name: 'desktop', version: app.getVersion() };
}

/**
 * The group servers the app knows: the one Aspire started in development, else those of `servers.json` in the app's
 * data folder. None when the settings are not valid: the window then says that no server is configured, and the log why.
 */
function registeredServers(): readonly ServerEntry[] {
  const settingsFile = join(process.env.LOCALAPPDATA ?? app.getPath('appData'), 'Voyager', 'servers.json');
  try {
    return readRegisteredServers({
      developmentUrl: developmentSetting('VOYAGER_SERVER_URL'),
      readSettingsFile: () =>
        app.isPackaged && existsSync(settingsFile) ? readFileSync(settingsFile, 'utf8') : undefined,
    });
  } catch (error) {
    // The message of the settings names the faulty entry by its position or identifier, never by its address; any other
    // error would name the path of the file, and so the user's account.
    desktopLog.error(
      'The servers could not be read: no server is used.',
      error instanceof ServerSettingsError ? error.message : 'The file servers.json could not be read.',
    );
    return [];
  }
}

/**
 * Lists the plugins of a server and makes their interfaces ready: from the app's own copy when it is the same, else
 * downloaded from the server and checked (interfaceModules.ts). Undefined when the server could not tell.
 */
async function readServedPlugins(server: ServerEntry): Promise<ServedPlugins | undefined> {
  const { name, version } = callingApp();
  const { pluginHost } = createServerClientsAt(
    server.url,
    { 'x-voyager-app': name, 'x-voyager-app-version': version },
    // A server that sends more than a file may weigh cannot fill the memory of the app.
    boundedFetch(
      (input, init) => net.fetch(input instanceof Request ? input : input.toString(), init),
      MAXIMUM_RESPONSE_BYTES,
    ),
  );
  let plugins;
  try {
    ({ plugins } = await pluginHost.listPlugins({}, { timeoutMs: LIST_PLUGINS_TIMEOUT_MS }));
  } catch (error) {
    // The code of the error, never its message, which may name the address of the server.
    desktopLog.error(
      `The list of plugins of the server ${server.id} could not be read (${Code[ConnectError.from(error).code]}): ` +
        'the plugin interfaces already served stay so, the others wait for a new reading.',
    );
    return undefined;
  }
  return prepareInterfaceModules(plugins, {
    shippedDirectory: shippedPluginsDirectory(),
    cacheDirectory: join(serversDataDirectory(), server.id, 'plugins'),
    download: async (plugin, path) =>
      (
        await pluginHost.getInterfaceFile(
          { pluginId: plugin.id, version: plugin.version, path },
          { timeoutMs: INTERFACE_FILE_TIMEOUT_MS },
        )
      ).content,
    log: {
      info: (message) => {
        desktopLog.info(`Server ${server.id}: ${message}`);
      },
      warn: (message) => {
        desktopLog.warn(`Server ${server.id}: ${message}`);
      },
    },
  });
}

/** Where the interface of each plugin of a server is, for the view of that server (servedPluginsReader.ts). */
function servedPluginsReader(server: ServerEntry | undefined): ServedPluginsReader {
  return createServedPluginsReader({
    read: async () => {
      if (server === undefined) {
        return new Map<string, string>();
      }
      try {
        return await readServedPlugins(server);
      } catch (error) {
        // The code of the error only: its message names the folder, and so the user's account. The next page that asks
        // for a plugin not served yet tries again.
        desktopLog.error(
          `The plugin interfaces of the server ${server.id} could not be made ready (${fileErrorCode(error)}).`,
        );
        return undefined;
      }
    },
    retryAfterMs: SERVED_PLUGINS_RETRY_MS,
  });
}

/** The code of a file error, such as EBUSY, without its message, which names the file. */
function fileErrorCode(error: unknown): string {
  const code = (error as { readonly code?: unknown } | null)?.code;
  return typeof code === 'string' ? code : 'unknown error';
}

/** Removes the folders of the servers the app no longer knows, with the interface modules downloaded from them. */
async function removeOtherServersData(servers: readonly ServerEntry[]): Promise<void> {
  const known = new Set(servers.map((server) => server.id));
  const directory = serversDataDirectory();
  const entries = await readdir(directory).catch(() => []);
  for (const entry of entries.filter((name) => !known.has(name))) {
    await rm(join(directory, entry), { recursive: true, force: true });
  }
}

/** The policy of the user interface: it allows the page's import map, which the plugins' modules rely on, by its hash. */
function contentSecurityPolicy(): string {
  // In development Vite serves the page, with no policy of ours: there is no built page to read.
  if (DEV_USER_INTERFACE_URL !== undefined) {
    return buildContentSecurityPolicy(undefined);
  }
  try {
    const page = readFileSync(join(userInterfaceDirectory(), 'index.html'), 'utf8');
    const importMapSource = findImportMapSource(page);
    if (importMapSource === undefined) {
      // Not an error of the page itself, but every plugin interface will be blocked: say it, or nobody will know why.
      desktopLog.warn('The user interface page has no import map: the interface of the plugins cannot load.');
    }
    return buildContentSecurityPolicy(importMapSource);
  } catch (error) {
    // Without the page there is nothing to show: the window will say so, and no script is allowed beyond the page's own.
    desktopLog.error('The user interface page could not be read:', describeError(error));
    return buildContentSecurityPolicy(undefined);
  }
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
    environment: app.isPackaged ? packagedServiceEnvironment() : pluginsEnvironment(),
    isolated: app.isPackaged,
  });
}

const WEB_PREFERENCES = {
  preload: join(import.meta.dirname, 'preload.cjs'),
  contextIsolation: true,
  sandbox: true,
  nodeIntegration: false,
  webSecurity: true,
  spellcheck: false,
} as const;

/** The address of a page of the user interface: the main screen, or the one shown while the app starts. */
function userInterfaceAddress(query = ''): string {
  return `${DEV_USER_INTERFACE_URL ?? APP_ENTRY_URL}${query}`;
}

/** A page that failed to load is logged, and its window is shown all the same: empty rather than invisible. */
function logLoadFailure(window: BaseWindow, error: unknown): void {
  desktopLog.error('A window could not load its page:', describeError(error));
  window.show();
}

/**
 * The main window, with one view per server, each in its own session (serverViews.ts); `onShown` runs once it is on
 * screen. A BaseWindow has no page of its own: the keyboard, the developer tools and reloading go to the view shown.
 */
function createWindow(
  servers: readonly ServerEntry[],
  onShown: () => void,
): { window: BaseWindow; views: ServerViews } {
  const window = new BaseWindow({ width: WINDOW_WIDTH, height: WINDOW_HEIGHT, show: false });
  const views = new ServerViews({
    host: {
      addView: (view) => {
        window.contentView.addChildView(view as WebContentsView);
      },
      contentSize: () => window.getContentBounds(),
      onResize: (listener) => {
        // Not every platform reports a resize when the window is maximized or goes full screen.
        for (const event of ['resize', 'maximize', 'unmaximize', 'enter-full-screen', 'leave-full-screen'] as const) {
          window.on(event as 'resize', listener);
        }
      },
      onClosed: (listener) => {
        window.once('closed', listener);
      },
    },
    servers,
    createView: (partition): ServerView => new WebContentsView({ webPreferences: { ...WEB_PREFERENCES, partition } }),
    url: userInterfaceAddress(),
    onFirstShown: () => {
      window.show();
      views.focusSelected();
      onShown();
    },
    onLoadFailure: (serverId, error) => {
      // The window opens when the view shown loads or fails (onFirstShown): a hidden view's failure is only logged.
      desktopLog.error(`The view of the server ${serverId ?? '(none)'} could not load its page:`, describeError(error));
    },
  });
  window.on('focus', () => {
    views.focusSelected();
  });
  return { window, views };
}

/** In development, reloading and the developer tools act on the view shown (the window has no page of its own). */
function setDevelopmentMenu(selectedView: () => ServerViews | undefined): void {
  Menu.setApplicationMenu(
    Menu.buildFromTemplate([
      {
        label: 'Development',
        submenu: [
          { label: 'Reload', accelerator: 'CmdOrCtrl+R', click: () => selectedView()?.reloadSelected() },
          {
            label: 'Developer tools',
            accelerator: 'CmdOrCtrl+Shift+I',
            click: () => selectedView()?.toggleDeveloperToolsOfSelected(),
          },
        ],
      },
    ]),
  );
}

/** The small window shown while the app looks for an update and starts its service. */
function createStartupWindow(): BrowserWindow {
  const window = new BrowserWindow({
    width: STARTUP_WINDOW_WIDTH,
    height: STARTUP_WINDOW_HEIGHT,
    show: false,
    frame: false,
    resizable: false,
    minimizable: false,
    maximizable: false,
    webPreferences: WEB_PREFERENCES,
  });
  window.once('ready-to-show', () => {
    window.show();
  });
  window.loadURL(userInterfaceAddress(STARTUP_SCREEN_QUERY)).catch((error: unknown) => {
    logLoadFailure(window, error);
  });
  return window;
}

/** Sends a message to a window, if it is still there and shows the user interface. */
function sendToWindow(window: BrowserWindow | undefined, channel: string, payload: unknown): void {
  if (window === undefined || window.isDestroyed() || window.webContents.isDestroyed()) {
    return;
  }
  if (isUserInterface(window.webContents.getURL())) {
    window.webContents.send(channel, payload);
  }
}

/**
 * The update server the package was built for, if any: it is written in the package.json of the app.
 * An unreadable file means no choice was made, which leaves the releases of the repository.
 */
function configuredUpdateSource(): unknown {
  try {
    const manifest = JSON.parse(readFileSync(join(app.getAppPath(), 'package.json'), 'utf8')) as Record<
      string,
      unknown
    >;
    return manifest[UPDATE_SOURCE_SETTING];
  } catch (error: unknown) {
    desktopLog.warn('The update server of the package could not be read:', describeError(error));
    return undefined;
  }
}

interface UpdateServer {
  readonly updates: UpdateManager;
  /** Where the updates come from when it is a server of the package's own; hidden from the log. */
  readonly address?: string;
}

/**
 * Where to look for updates: the server the package was built for, else the releases of the repository.
 * @throws when the app is not installed by its installer.
 */
function createUpdateServer(): UpdateServer {
  const choice = chooseUpdateSource(configuredUpdateSource());
  if (choice.kind === 'http') {
    desktopLog.info('Updates come from the server the package was built for.');
    // No time limit on the source itself: it would apply to the download of the package too.
    return { updates: new UpdateManager(new HttpSource(choice.url)), address: choice.url };
  }
  if (choice.kind === 'invalid') {
    desktopLog.warn(`${choice.reason} Updates come from the releases of the repository.`);
  }
  // A version with a suffix (0.3.0-beta.2) comes from the beta channel: its releases are pre-releases.
  const prerelease = app.getVersion().includes('-');
  return { updates: new UpdateManager(new GithubSource(RELEASES_REPOSITORY_URL, undefined, prerelease)) };
}

/**
 * Looks for a newer version of the installed app and, if there is one, installs it: the app then
 * restarts. An app run from a plain folder, or in development, has nothing to update.
 */
async function updateInstalledApp(report: (event: UpdateEvent) => void): Promise<UpdateOutcome> {
  if (!app.isPackaged) {
    return 'up-to-date';
  }
  let server: UpdateServer;
  try {
    server = createUpdateServer();
  } catch (error: unknown) {
    desktopLog.info('Updates are off: the app is not installed by its installer.', describeError(error));
    return 'up-to-date';
  }
  // Errors of the update client often quote the address it was reaching.
  const log = {
    info: (message: string) => {
      desktopLog.info(message);
    },
    warn: (message: string) => {
      desktopLog.warn(hideAddress(message, server.address));
    },
  };
  return updateApp(server.updates, log, report);
}

function start(): void {
  let startupState: StartupState = { status: 'checking-updates' };
  // Only started once the window is open: see runStartup.
  let localService: LocalServiceSupervisor | undefined;
  let startupWindow: BrowserWindow | undefined;
  let mainWindow: BaseWindow | undefined;
  // The group servers the page may call (spec of the switch to client-server, "Les serveurs enregistrés").
  const servers = registeredServers();
  // The views of the main window, one per server; undefined until it opens.
  let views: ServerViews | undefined;
  // Set as soon as the app starts to quit: a startup still going on must then start nothing.
  let quitting = false;
  app.on('before-quit', () => {
    quitting = true;
  });

  // The local service still runs until lot 3 of the switch removes it: nothing calls it any more.
  const beginLocalService = (): void => {
    if (quitting) {
      return;
    }
    const supervisor = superviseLocalService({
      launch: startLocalService,
      report: (message) => {
        desktopLog.error(message);
      },
      inform: (message) => {
        desktopLog.info(message);
      },
    });
    localService = supervisor;
  };

  // Quitting waits for the local service to stop, so that it never writes to a database left behind.
  let localServiceStopped = false;
  app.on('will-quit', (event) => {
    if (localServiceStopped || localService === undefined) {
      return;
    }
    event.preventDefault();
    void localService.stop().finally(() => {
      localServiceStopped = true;
      app.quit();
    });
  });

  /**
   * Only the Voyager user interface may ask. `view`: only a view of the main window. Managing plugins on this machine is
   * answered by no one: the plugins run on the server (ADR-0020), and a module downloaded from a server must reach
   * nothing but its server.
   */
  const checkCaller = (event: IpcMainInvokeEvent, scope: 'any' | 'view' = 'any'): void => {
    if (!isUserInterface(event.senderFrame?.url ?? '')) {
      throw new Error('Only the Voyager user interface can ask the desktop app for this.');
    }
    if (scope === 'view' && views?.owns(event.sender.id) !== true) {
      throw new Error('Only the main window of the Voyager user interface can ask the desktop app for this.');
    }
  };
  ipcMain.handle(GET_STARTUP_STATE_CHANNEL, (event) => {
    checkCaller(event);
    return startupState;
  });
  ipcMain.handle(GET_APP_VERSION_CHANNEL, (event) => {
    checkCaller(event, 'view');
    return app.getVersion();
  });
  // Every view learns the identifiers of the servers, for the selector; never the addresses of the others, which the
  // interface modules of its own server would otherwise read.
  ipcMain.handle(GET_SERVERS_CHANNEL, (event): readonly ServerChoice[] => {
    checkCaller(event, 'view');
    return servers.map((server) => ({ id: server.id }));
  });
  // Each view belongs to one server: the page learns which, with its address, and shows only that one.
  ipcMain.handle(GET_CURRENT_SERVER_CHANNEL, (event): RegisteredServer | undefined => {
    checkCaller(event, 'view');
    const server = views?.serverOf(event.sender.id);
    return server === undefined ? undefined : { id: server.id, address: server.url };
  });
  // Only the view shown may show another, by the selector the user sees: a hidden view cannot take the screen.
  ipcMain.handle(SELECT_SERVER_CHANNEL, (event, serverId: unknown): void => {
    checkCaller(event, 'view');
    if (views?.serverOf(event.sender.id)?.id !== views?.selected) {
      throw new Error('Only the server shown can show another one.');
    }
    if (typeof serverId !== 'string' || views?.select(serverId) !== true) {
      throw new Error('The app knows no such server.');
    }
  });

  lockDownWebContents(isUserInterface);
  app.on('window-all-closed', () => {
    app.quit();
  });

  void app.whenReady().then(async () => {
    // In development the page comes from Vite: its origin may call the servers through the app's protocol too.
    const developmentOrigin =
      DEV_USER_INTERFACE_URL === undefined ? undefined : URL.parse(DEV_USER_INTERFACE_URL)?.origin;
    const policy = contentSecurityPolicy();
    const allowedOrigins = developmentOrigin === undefined ? [] : [developmentOrigin];
    const serveFor = (target: Pick<typeof protocol, 'handle'>, server: ServerEntry | undefined): void => {
      const plugins = servedPluginsReader(server);
      // Read as the view opens: the page finds its plugins ready instead of waiting for their download.
      if (server !== undefined) {
        plugins.refresh();
      }
      serveUserInterface(target, {
        directory: userInterfaceDirectory(),
        // The session of a view serves the plugins of its own server only.
        servedPlugins: plugins.find,
        contentSecurityPolicy: policy,
        allowedOrigins,
        forwardToServer: (request) =>
          handleServerApiRequest(request, {
            // The session of a view forwards to its own server only: a module of another server cannot reach it.
            servers: () => (server === undefined ? [] : [server]),
            app: callingApp(),
            allowedOrigins,
            fetch: (url, init) => net.fetch(url, init),
            log: {
              warn: (message) => {
                desktopLog.warn(message);
              },
            },
          }),
      });
    };
    // The default session shows the startup window: it calls no server.
    denyAllPermissions();
    serveFor(protocol, undefined);
    for (const server of servers.length === 0 ? [undefined] : servers) {
      const serverSession = session.fromPartition(serverPartition(server?.id));
      denyAllPermissions(serverSession);
      serveFor(serverSession.protocol, server);
    }
    if (servers.length > 0) {
      void removeOtherServersData(servers).catch((error: unknown) => {
        desktopLog.warn(
          `The data of the servers the app no longer knows could not be removed (${fileErrorCode(error)}).`,
        );
      });
    }

    // Opening Voyager again brings back the window already open.
    app.on('second-instance', () => {
      const window = [mainWindow, startupWindow].find(
        (candidate) => candidate !== undefined && !candidate.isDestroyed(),
      );
      if (window?.isMinimized() === true) {
        window.restore();
      }
      window?.focus();
      views?.focusSelected();
    });
    if (!app.isPackaged) {
      setDevelopmentMenu(() => views);
    }

    // Development goes straight to the window: nothing to wait for, a flash of screen would only be noise.
    if (app.isPackaged || developmentSetting('VOYAGER_SHOW_STARTUP_SCREEN') !== undefined) {
      const startup = createStartupWindow();
      startup.once('closed', () => {
        startupWindow = undefined;
      });
      startupWindow = startup;
    }
    const openMainWindow = (): void => {
      if (quitting) {
        return;
      }
      // The startup window stays until the main one is on screen: never a moment without a window.
      const main = createWindow(servers, () => {
        startupWindow?.close();
      });
      main.window.once('closed', () => {
        mainWindow = undefined;
        views = undefined;
      });
      mainWindow = main.window;
      views = main.views;
    };

    try {
      const result = await runStartup({
        updateApp: updateInstalledApp,
        report: (state) => {
          startupState = state;
          sendToWindow(startupWindow, STARTUP_STATE_CHANGED_CHANNEL, state);
        },
        openMainWindow,
      });
      if (result === 'restarting-for-update') {
        desktopLog.info('The app quits so that the update can be installed.');
        // The updater waits for this app to exit, installs the new version and starts it.
        app.quit();
      } else {
        beginLocalService();
      }
    } catch (error: unknown) {
      // The window still opens: its own screens say what is wrong with each server.
      desktopLog.error('The app could not start normally:', describeError(error));
      if (mainWindow === undefined) {
        openMainWindow();
      }
    }
  });
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
registerAppScheme(!app.isPackaged);
app.enableSandbox();

// Two instances would share, and fight over, the same local database.
if (app.requestSingleInstanceLock()) {
  start();
} else {
  desktopLog.info('Voyager is already running; this second instance quits.');
  app.quit();
}
