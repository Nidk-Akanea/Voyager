// Main process of the desktop app: looks for an update while a small startup window is shown (ADR-0014),
// keeps the local service running until the switch to client-server is over, opens the main window on the user
// interface, and forwards the calls of the page to its group servers (ADR-0018, ADR-0019).
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

import {
  app,
  BaseWindow,
  BrowserWindow,
  dialog,
  ipcMain,
  Menu,
  net,
  protocol,
  session,
  shell,
  WebContentsView,
  type IpcMainInvokeEvent,
  type MessageBoxOptions,
  type OpenDialogOptions,
} from 'electron';
import { GithubSource, HttpSource, UpdateManager, VelopackApp } from 'velopack';

import {
  Code,
  ConnectError,
  createLocalServiceClients,
  createServerClientsAt,
  PluginOrigin,
  type LocalServiceConnection,
  type PluginTarget,
  type RegisteredServer,
  type ServerChoice,
  type RestartResult,
  type StartupState,
} from '@voyager/grpc-client';

import {
  ADD_PACKAGE_CHANNEL,
  CANCEL_PLUGIN_REQUEST_CHANNEL,
  DISABLE_PLUGIN_CHANNEL,
  ENABLE_PLUGIN_CHANNEL,
  GET_APP_VERSION_CHANNEL,
  GET_CURRENT_SERVER_CHANNEL,
  GET_SERVERS_CHANNEL,
  GET_STARTUP_STATE_CHANNEL,
  INSTALL_PLUGIN_CHANNEL,
  LIST_INBOX_CHANNEL,
  LIST_PLUGIN_REQUESTS_CHANNEL,
  OPEN_INBOX_CHANNEL,
  RESTART_FOR_PLUGINS_CHANNEL,
  SELECT_SERVER_CHANNEL,
  START_WITHOUT_INSTALLED_PLUGINS_CHANNEL,
  STARTUP_STATE_CHANGED_CHANNEL,
  UNINSTALL_PLUGIN_CHANNEL,
} from '../shared/ipc';
import { APP_ENTRY_URL, isAppUrl, registerAppScheme, serveUserInterface } from './appProtocol';
import { buildContentSecurityPolicy, findImportMapSource } from './contentSecurityPolicy';
import { describeError } from './describeError';
import { launchLocalService, attachToRunningLocalService, type LocalService } from './localService';
import { superviseLocalService, type LocalServiceSupervisor } from './localServiceSupervisor';
import { configureLogging, desktopLog } from './logging';
import { packageFileDialog, pickDialogLocale, type ConfirmationBox } from './plugins/confirmations';
import { PACKAGE_EXTENSION } from './plugins/inbox';
import { createPluginManagement, type PluginManagementFolders } from './plugins/pluginManagement';
import { denyAllPermissions, lockDownWebContents } from './security';
import { servedPluginFolders, type ServedPlugins } from './servedPlugins';
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

// How long the main process waits for the list of plugins: the files of the plugins wait for it.
const LIST_PLUGINS_TIMEOUT_MS = 10_000;
// How long after a failed reading of the list of plugins the next file of a plugin asks the server again.
const SERVED_PLUGINS_RETRY_MS = 5_000;
// How long the examination of a package may take: no size limit, so a package of data may be large (spec).
const EXAMINE_PACKAGE_TIMEOUT_MS = 10 * 60_000;

/** The files the main process uses in the plugins folder of the user's data (ADR-0016, decisions 3 and 4). */
function pluginManagementFolders(): PluginManagementFolders {
  const folder = userPluginsDirectory();
  return {
    inbox: join(folder, 'inbox'),
    staging: join(folder, 'staging'),
    requestsFile: join(folder, 'requests.json'),
    stateFile: join(folder, 'state.json'),
  };
}

/** The options of Electron's message box for a confirmation: nothing but what the box says. */
function messageBoxOptions(box: ConfirmationBox): MessageBoxOptions {
  return {
    type: box.type,
    title: box.title,
    message: box.message,
    detail: box.detail,
    buttons: [...box.buttons],
    defaultId: box.defaultId,
    cancelId: box.cancelId,
    noLink: box.noLink,
    ...(box.checkboxLabel === undefined ? {} : { checkboxLabel: box.checkboxLabel, checkboxChecked: false }),
  };
}

/**
 * Restarts the local service so that it applies the requests of the user, and resolves once the new service is ready
 * or has failed: until then it may be reading the requests, and none may change.
 */
async function restartToApplyRequests(supervisor: LocalServiceSupervisor): Promise<RestartResult> {
  let stopListening = (): void => undefined;
  const settled = new Promise<void>((resolve) => {
    stopListening = supervisor.onChange((state) => {
      if (state.status === 'ready' || state.status === 'failed') {
        resolve();
      }
    });
  });
  try {
    if (!(await supervisor.restart())) {
      return { outcome: 'restart-elsewhere' };
    }
    await settled;
    return { outcome: 'restarting' };
  } finally {
    stopListening();
  }
}

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
 * The folder of each plugin the server loaded, whose interface module the app ships (the modules come from the server
 * with lot 2c of the switch); undefined when it cannot tell, and nothing is served then.
 */
async function listServedPlugins(server: ServerEntry): Promise<ServedPlugins | undefined> {
  const { name, version } = callingApp();
  try {
    const { plugins } = await createServerClientsAt(
      server.url,
      { 'x-voyager-app': name, 'x-voyager-app-version': version },
      (input, init) => net.fetch(input instanceof Request ? input : input.toString(), init),
    ).pluginHost.listPlugins({}, { timeoutMs: LIST_PLUGINS_TIMEOUT_MS });
    // Only the shipped plugins, whose modules the app has: a plugin installed on the server has none here, and the
    // folders of the management on this machine never serve it (the modules come from the server with lot 2c).
    const shipped = plugins.filter((plugin) => plugin.origin === PluginOrigin.SHIPPED);
    return servedPluginFolders(shipped, {
      shipped: shippedPluginsDirectory(),
      installed: join(userPluginsDirectory(), 'installed'),
    });
  } catch (error) {
    // The code of the error, never its message, which may name the address of the server.
    desktopLog.error(
      `The list of plugins of the server ${server.id} could not be read (${Code[ConnectError.from(error).code]}): ` +
        'no plugin interface is served until it can be.',
    );
    return undefined;
  }
}

/**
 * Reads, for the view of a server, the folders of the plugins whose interface it may serve: once, then again at the next
 * file only when the reading failed, never more than once at a time, and not sooner than a few seconds after the last
 * try. A plugin enabled on the server shows its interface at the next start of the app, until lot 2c.
 */
function servedPluginsReader(server: ServerEntry | undefined): () => Promise<ServedPlugins> {
  let servedPlugins: Promise<ServedPlugins | undefined> | undefined;
  let lastFailure = 0;
  return async () => {
    if (server === undefined) {
      return new Map<string, string>();
    }
    if (servedPlugins === undefined && Date.now() - lastFailure >= SERVED_PLUGINS_RETRY_MS) {
      const reading = listServedPlugins(server);
      servedPlugins = reading;
      void reading.then((served) => {
        if (served === undefined) {
          lastFailure = Date.now();
          servedPlugins = undefined;
        }
      });
    }
    return (await servedPlugins) ?? new Map<string, string>();
  };
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
  // Only created once no update is on its way: see runStartup.
  let localService: LocalServiceSupervisor | undefined;
  let startupWindow: BrowserWindow | undefined;
  let mainWindow: BaseWindow | undefined;
  // The group servers the page may call (spec of the switch to client-server, "Les serveurs enregistrés").
  const servers = registeredServers();
  // The views of the main window, one per server; undefined until it opens.
  let views: ServerViews | undefined;
  // The local service still runs until lot 3 of the switch, for the management of plugins on this machine, which the
  // window no longer shows nor calls.
  let serviceConnection: LocalServiceConnection | undefined;
  // Set as soon as the app starts to quit: a startup still going on must then start nothing.
  let quitting = false;
  app.on('before-quit', () => {
    quitting = true;
  });

  const beginLocalService = (): Promise<void> => {
    if (quitting) {
      return Promise.resolve();
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
    // The window no longer talks to the local service: its address and token stay in this process.
    supervisor.onChange((state) => {
      if (state.status === 'ready') {
        serviceConnection = state.connection;
      }
    });
    return supervisor.whenSettled();
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
   * Only the Voyager user interface may ask. `view`: only a view of the main window; `first-view`: only the view of the
   * first server, the one that managed plugins on this machine before the views (until lot 3 removes that management).
   */
  const checkCaller = (event: IpcMainInvokeEvent, scope: 'any' | 'view' | 'first-view' = 'any'): void => {
    if (!isUserInterface(event.senderFrame?.url ?? '')) {
      throw new Error('Only the Voyager user interface can ask the desktop app for this.');
    }
    const fromView = views?.owns(event.sender.id) === true;
    const fromFirstView = fromView && views?.serverOf(event.sender.id)?.id === servers.at(0)?.id;
    if ((scope === 'view' && !fromView) || (scope === 'first-view' && !fromFirstView)) {
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

  // The dialogs belong to the main window, so that the page waits for the answer.
  const parentWindow = (): BaseWindow | undefined =>
    mainWindow !== undefined && !mainWindow.isDestroyed() ? mainWindow : undefined;
  const dialogLocale = () => pickDialogLocale(app.getPreferredSystemLanguages());
  const pluginHost = () => {
    if (serviceConnection === undefined) {
      throw new Error('The local service is not ready.');
    }
    return createLocalServiceClients(serviceConnection).pluginHost;
  };
  const plugins = createPluginManagement({
    folders: pluginManagementFolders(),
    listPlugins: async () => (await pluginHost().listPlugins({}, { timeoutMs: LIST_PLUGINS_TIMEOUT_MS })).plugins,
    examinePackage: (stagedName) =>
      pluginHost().examinePackage({ stagedName }, { timeoutMs: EXAMINE_PACKAGE_TIMEOUT_MS }),
    showDialog: async (box) => {
      const parent = parentWindow();
      const options = messageBoxOptions(box);
      const answer =
        parent === undefined ? await dialog.showMessageBox(options) : await dialog.showMessageBox(parent, options);
      return { response: answer.response, checkboxChecked: answer.checkboxChecked };
    },
    chooseFile: async () => {
      const { title, filterName } = packageFileDialog(dialogLocale());
      const options: OpenDialogOptions = {
        title,
        properties: ['openFile'],
        filters: [{ name: filterName, extensions: [PACKAGE_EXTENSION.slice(1)] }],
      };
      const parent = parentWindow();
      const result =
        parent === undefined ? await dialog.showOpenDialog(options) : await dialog.showOpenDialog(parent, options);
      return result.canceled ? undefined : result.filePaths[0];
    },
    openFolder: async (folder) => {
      // An empty text when the folder opened, else what went wrong, which names the folder.
      if ((await shell.openPath(folder)) !== '') {
        throw new Error('The file explorer could not open the folder.');
      }
    },
    restartService: () =>
      localService === undefined ? Promise.resolve({ outcome: 'failed' }) : restartToApplyRequests(localService),
    isServiceReady: () => localService?.state().status === 'ready',
    isServiceFailed: () => localService?.state().status === 'failed',
    retryService: () => {
      localService?.retry();
    },
    locale: dialogLocale,
    log: {
      info: (message) => {
        desktopLog.info(message);
      },
      warn: (message) => {
        desktopLog.warn(message);
      },
      error: (message) => {
        desktopLog.error(message);
      },
    },
  });
  // Only the view of the first server manages plugins on this machine (no longer shown, removed by lot 3); what the page
  // sends is checked by the plugin management itself.
  const handlePlugins = (channel: string, handler: (argument: unknown) => Promise<unknown>): void => {
    ipcMain.handle(channel, (event, argument: unknown) => {
      checkCaller(event, 'first-view');
      return handler(argument);
    });
  };
  handlePlugins(LIST_INBOX_CHANNEL, () => plugins.listInbox());
  handlePlugins(ADD_PACKAGE_CHANNEL, () => plugins.addPackage());
  handlePlugins(OPEN_INBOX_CHANNEL, () => plugins.openInbox());
  // The casts only name what the page should send: each value is checked before use.
  handlePlugins(INSTALL_PLUGIN_CHANNEL, (name) => plugins.install(name as string));
  handlePlugins(UNINSTALL_PLUGIN_CHANNEL, (target) => plugins.uninstall(target as PluginTarget));
  handlePlugins(ENABLE_PLUGIN_CHANNEL, (target) => plugins.enable(target as PluginTarget));
  handlePlugins(DISABLE_PLUGIN_CHANNEL, (target) => plugins.disable(target as PluginTarget));
  handlePlugins(CANCEL_PLUGIN_REQUEST_CHANNEL, (target) => plugins.cancelRequest(target as PluginTarget));
  handlePlugins(LIST_PLUGIN_REQUESTS_CHANNEL, () => plugins.listRequests());
  handlePlugins(RESTART_FOR_PLUGINS_CHANNEL, () => plugins.restart());
  // Only from the screen of a service that failed, which is not running and so reads no request meanwhile.
  handlePlugins(START_WITHOUT_INSTALLED_PLUGINS_CHANNEL, () => plugins.startWithoutInstalledPlugins());

  lockDownWebContents(isUserInterface);
  app.on('window-all-closed', () => {
    app.quit();
  });

  void app.whenReady().then(async () => {
    // In development the page comes from Vite: its origin may call the servers through the app's protocol too.
    const developmentOrigin =
      DEV_USER_INTERFACE_URL === undefined ? undefined : URL.parse(DEV_USER_INTERFACE_URL)?.origin;
    const policy = contentSecurityPolicy();
    const serveFor = (target: Pick<typeof protocol, 'handle'>, server: ServerEntry | undefined): void => {
      serveUserInterface(target, userInterfaceDirectory(), servedPluginsReader(server), policy, (request) =>
        handleServerApiRequest(request, {
          // The session of a view forwards to its own server only: a module of another server cannot reach it.
          servers: () => (server === undefined ? [] : [server]),
          app: callingApp(),
          allowedOrigins: developmentOrigin === undefined ? [] : [developmentOrigin],
          fetch: (url, init) => net.fetch(url, init),
          log: {
            warn: (message) => {
              desktopLog.warn(message);
            },
          },
        }),
      );
    };
    // The default session shows the startup window: it calls no server.
    denyAllPermissions();
    serveFor(protocol, undefined);
    for (const server of servers.length === 0 ? [undefined] : servers) {
      const serverSession = session.fromPartition(serverPartition(server?.id));
      denyAllPermissions(serverSession);
      serveFor(serverSession.protocol, server);
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
        startLocalService: beginLocalService,
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
      }
    } catch (error: unknown) {
      // The window still opens: its own screens say what is wrong with the local service.
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
