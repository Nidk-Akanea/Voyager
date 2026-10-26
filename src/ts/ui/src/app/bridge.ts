// What the desktop app exposes to the user interface through its preload script (contextBridge).
// The user interface never reaches Node.js or the file system directly (docs/conventions/typescript.md).
import type { LocalServiceState, StartupState } from '@voyager/grpc-client';

export interface VoyagerBridge {
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
}

/** What the main screens need from the desktop app: the state of the local service, and a way to restart it. */
export type LocalServiceBridge = Pick<
  VoyagerBridge,
  'getLocalServiceState' | 'onLocalServiceStateChange' | 'retryLocalService'
>;

/** What the startup screen needs from the desktop app. */
export type StartupBridge = Pick<VoyagerBridge, 'getStartupState' | 'onStartupStateChange'>;

declare global {
  interface Window {
    /** Present only inside the desktop app. */
    readonly voyager?: VoyagerBridge;
  }
}
