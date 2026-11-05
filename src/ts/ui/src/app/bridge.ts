// What the desktop app exposes to the user interface through its preload script (contextBridge).
// The user interface never reaches Node.js or the file system directly (docs/conventions/typescript.md).
import type {
  LocalServiceState,
  PluginManagementBridge,
  RecoveryResult,
  RegisteredServer,
  StartupState,
} from '@voyager/grpc-client';

export interface VoyagerBridge {
  /** The group servers the app knows, by identifier and address; the page calls them through the main process. */
  readonly getServers: () => Promise<readonly RegisteredServer[]>;
  /** The version of the app, for the "About" screen. */
  readonly getAppVersion: () => Promise<string>;
  /** The server of this view (each view of the window shows one server); undefined when the app knows none. */
  readonly getCurrentServer: () => Promise<string | undefined>;
  /** Shows the view of another server; refused for a server the app does not know. */
  readonly selectServer: (serverId: string) => Promise<void>;
  /** Where the local service stands: starting, ready (with its address and secret), restarting or failed. */
  readonly getLocalServiceState: () => Promise<LocalServiceState>;
  /** Calls the listener at every change of the local service; returns the function that stops it. */
  readonly onLocalServiceStateChange: (listener: (state: LocalServiceState) => void) => () => void;
  /** What the app is doing before its window opens: looking for an update, then starting the service. */
  readonly getStartupState: () => Promise<StartupState>;
  /** Calls the listener at every change of the startup state; returns the function that stops it. */
  readonly onStartupStateChange: (listener: (state: StartupState) => void) => () => void;
  /** Starts the local service again after it failed. */
  readonly retryLocalService: () => Promise<void>;
  /**
   * Starts the local service again after it failed, with every installed plugin disabled: the user then enables them
   * again one by one to find the one that crashes Voyager.
   */
  readonly startWithoutInstalledPlugins: () => Promise<RecoveryResult>;
  /** Manages the plugins: the inbox, the requests, and the restart that applies them (ADR-0016). */
  readonly plugins: PluginManagementBridge;
}

/** What the main screens need from the desktop app: the state of the local service, and a way to restart it. */
export type LocalServiceBridge = Pick<
  VoyagerBridge,
  'getLocalServiceState' | 'onLocalServiceStateChange' | 'retryLocalService' | 'startWithoutInstalledPlugins'
>;

/** What the main screens need to reach the group servers. */
export type ServerBridge = Pick<VoyagerBridge, 'getServers' | 'getAppVersion' | 'getCurrentServer' | 'selectServer'>;

/** What the startup screen needs from the desktop app. */
export type StartupBridge = Pick<VoyagerBridge, 'getStartupState' | 'onStartupStateChange'>;

declare global {
  interface Window {
    /** Present only inside the desktop app. */
    readonly voyager?: VoyagerBridge;
  }
}
