// What the desktop app exposes to the user interface through its preload script (contextBridge).
// The user interface never reaches Node.js or the file system directly (docs/conventions/typescript.md).
import type { RegisteredServer, ServerChoice, StartupState } from '@voyager/grpc-client';

export interface VoyagerBridge {
  /** The identifiers of the group servers the app knows, for the selector; the page calls them through the main process. */
  readonly getServers: () => Promise<readonly ServerChoice[]>;
  /** The version of the app, for the "About" screen. */
  readonly getAppVersion: () => Promise<string>;
  /** The server of this view, with its address (each view of the window shows one server); undefined when none. */
  readonly getCurrentServer: () => Promise<RegisteredServer | undefined>;
  /** Shows the view of another server; refused for a server the app does not know. */
  readonly selectServer: (serverId: string) => Promise<void>;
  /** What the app is doing before its window opens: looking for an update, downloading or installing it. */
  readonly getStartupState: () => Promise<StartupState>;
  /** Calls the listener at every change of the startup state; returns the function that stops it. */
  readonly onStartupStateChange: (listener: (state: StartupState) => void) => () => void;
}

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
