// What the desktop app exposes to the user interface through its preload script (contextBridge).
// The user interface never reaches Node.js or the file system directly (docs/conventions/typescript.md).
import type { LocalServiceConnection } from '@voyager/grpc-client';

export interface VoyagerBridge {
  /** Where the local service listens, and the secret it expects. */
  readonly getLocalService: () => Promise<LocalServiceConnection>;
}

declare global {
  interface Window {
    /** Present only inside the desktop app. */
    readonly voyager?: VoyagerBridge;
  }
}
