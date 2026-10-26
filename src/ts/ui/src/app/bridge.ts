// What the desktop app exposes to the user interface through its preload script (contextBridge).
// The user interface never reaches Node.js or the file system directly (docs/conventions/typescript.md).
import type { LocalServiceState } from '@voyager/grpc-client';

export interface VoyagerBridge {
  /** Where the local service stands: starting, ready (with its address and secret), restarting or failed. */
  readonly getLocalServiceState: () => Promise<LocalServiceState>;
  /** Calls the listener at every change of the local service; returns the function that stops it. */
  readonly onLocalServiceStateChange: (listener: (state: LocalServiceState) => void) => () => void;
  /** Starts the local service again after it failed. */
  readonly retryLocalService: () => Promise<void>;
}

declare global {
  interface Window {
    /** Present only inside the desktop app. */
    readonly voyager?: VoyagerBridge;
  }
}
